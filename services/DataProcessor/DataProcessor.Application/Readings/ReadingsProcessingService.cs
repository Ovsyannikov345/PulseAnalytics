using Ausiannikau.Utilities.Results;
using DataProcessor.Application.Interfaces.Database;
using DataProcessor.Application.Readings.Models;
using DataProcessor.Application.Readings.Strategies;
using DataProcessor.Domain.Constants.Enums;
using DataProcessor.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DataProcessor.Application.Readings;

public interface IReadingsProcessingService
{
    public Task<Result> ProcessAsync(MeterReadingModel reading, CancellationToken ct);
}

internal sealed class ReadingsProcessingService(
    IEnumerable<IReadingProcessingStrategy> strategies,
    IApplicationDbContext dbContext,
    ILogger<ReadingsProcessingService> logger)
    : IReadingsProcessingService
{
    private readonly Dictionary<Type, IReadingProcessingStrategy> _strategies =
        strategies.ToDictionary(strategy => strategy.ModelType);

    public async Task<Result> ProcessAsync(MeterReadingModel reading, CancellationToken ct)
    {
        var modelType = reading.GetType();

        if (!_strategies.TryGetValue(modelType, out var strategy))
        {
            logger.NoStrategyForReading(modelType.Name);

            return Result.Fail($"No processing strategy for {modelType.Name}");
        }

        var isDuplicate = await dbContext.Query<MeterReading>()
            .AnyAsync(storedReading => storedReading.DeduplicationKey == reading.DeduplicationKey, ct);

        if (isDuplicate)
        {
            logger.DuplicateReadingSkipped(reading.DeduplicationKey, reading.Location);

            return Result.Succeed();
        }

        var creationResult = strategy.CreateReading(reading);

        if (!creationResult.TryGetSuccessValue(out var meterReading))
        {
            logger.FailedToCreateReading(modelType.Name, creationResult.ErrorMessage);

            return creationResult;
        }

        dbContext.Add(meterReading);

        await dbContext.SaveChangesAsync(ct);

        logger.ReadingStored(meterReading.Type, meterReading.Location, meterReading.DeduplicationKey);

        return Result.Succeed();
    }
}

internal static partial class ReadingsProcessingServiceLogs
{
    [LoggerMessage(Level = LogLevel.Error, Message = "No processing strategy is registered for {ModelType}")]
    public static partial void NoStrategyForReading(this ILogger logger, string modelType);

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Reading {DeduplicationKey} for location {Location} is already stored, skipped")]
    public static partial void DuplicateReadingSkipped(this ILogger logger, Guid deduplicationKey, MeterLocation location);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to create a reading from {ModelType}. Reason: {Reason}")]
    public static partial void FailedToCreateReading(this ILogger logger, string modelType, string reason);

    [LoggerMessage(
        Level = LogLevel.Debug,
        Message = "Stored {MeterType} reading {DeduplicationKey} for location {Location}")]
    public static partial void ReadingStored(this ILogger logger, MeterType meterType, MeterLocation location, Guid deduplicationKey);
}
