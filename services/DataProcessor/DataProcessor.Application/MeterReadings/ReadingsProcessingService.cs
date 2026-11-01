using Ausiannikau.Utilities.Results;
using DataProcessor.Application.Interfaces.Database;
using DataProcessor.Application.MeterReadings.Models;
using DataProcessor.Application.MeterReadings.Strategies;
using DataProcessor.Domain.Constants.Enums;
using DataProcessor.Domain.Entities.MeterReadings;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DataProcessor.Application.MeterReadings;

public interface IReadingsProcessingService
{
    public Task<Result> ProcessAsync(MeterReadingModel reading, CancellationToken ct);
}

internal sealed class ReadingsProcessingService(
    IEnumerable<IReadingProcessingStrategy> processingStrategies,
    ILogger<ReadingsProcessingService> logger)
    : IReadingsProcessingService
{
    public async Task<Result> ProcessAsync(MeterReadingModel reading, CancellationToken ct)
    {
        var processingStrategy = processingStrategies.FirstOrDefault(s => s.CanProcess(reading));

        if (processingStrategy is null)
        {
            var modelType = reading.GetType();

            logger.NoStrategyForReading(modelType.Name);

            return Result.Fail($"No processing strategy defined for {modelType.Name}");
        }

        return await processingStrategy.ProcessAsync(reading, ct);
    }
}

internal static partial class ReadingsProcessingServiceLogs
{
    [LoggerMessage(Level = LogLevel.Error, Message = "No processing strategy is registered for {ModelType}")]
    public static partial void NoStrategyForReading(this ILogger logger, string modelType);
}
