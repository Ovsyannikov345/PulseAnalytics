using Ausiannikau.Utilities.Results;
using DataProcessor.Application.Interfaces.Database;
using DataProcessor.Application.MeterReadings.Models;
using DataProcessor.Domain.Constants.Enums;
using DataProcessor.Domain.Entities.MeterReadings;
using DataProcessor.Domain.Entities.MeterReadings.Payloads;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DataProcessor.Application.MeterReadings.Strategies;

internal interface IReadingProcessingStrategy
{
    public bool CanProcess(MeterReadingModel model);

    public Task<Result> ProcessAsync(MeterReadingModel model, CancellationToken ct);
}

internal abstract class ReadingProcessingStrategy<TModel>(IApplicationDbContext dbContext, ILogger logger) : IReadingProcessingStrategy
    where TModel : MeterReadingModel
{
    protected abstract MeterType MeterType { get; }

    public abstract bool CanProcess(MeterReadingModel model);

    public async Task<Result> ProcessAsync(MeterReadingModel model, CancellationToken ct)
    {
        if (model is not TModel typedModel)
        {
            return Result<MeterReading>.Fail($"{GetType().Name} cannot process a {model.GetType().Name}");
        }

        var isDuplicate = await dbContext.Query<MeterReading>().AnyAsync(r => r.DeduplicationKey == model.DeduplicationKey, ct);

        if (isDuplicate)
        {
            logger.DuplicateReadingSkipped(model.DeduplicationKey, model.Location);

            return Result.Succeed();
        }

        dbContext.Add(new MeterReading
        {
            DeduplicationKey = typedModel.DeduplicationKey,
            Type = MeterType,
            Location = typedModel.Location,
            ReceivedAt = typedModel.ReceivedAt,
            Payload = CreatePayload(typedModel)
        });

        await dbContext.SaveChangesAsync(ct);

        logger.ReadingStored(MeterType, model.Location, model.DeduplicationKey);

        return Result.Succeed();

    }

    protected abstract ReadingPayload CreatePayload(TModel model);
}

internal static partial class ReadingProcessingStrategyLogs
{
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
