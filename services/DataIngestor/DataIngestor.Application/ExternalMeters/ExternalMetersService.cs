using Ausiannikau.Utilities.Results;
using DataIngestor.Application.Interfaces.Clients;
using DataIngestor.Application.Interfaces.Messaging;
using DataIngestor.Application.Telemetry.Metrics;
using DataIngestor.Application.Telemetry.Metrics.Enums;
using Microsoft.Extensions.Logging;

namespace DataIngestor.Application.ExternalMeters;

public interface IExternalMetersService
{
    public Task<Result> IngestReadingsAsync(CancellationToken ct);
}

internal sealed class ExternalMetersService(
    IExternalMetersClient metersClient,
    IMeterReadingsPublisher readingsPublisher,
    IMetricsService metrics,
    ILogger<ExternalMetersService> logger)
    : IExternalMetersService
{
    public async Task<Result> IngestReadingsAsync(CancellationToken ct)
    {
        var extractionResult = await metersClient.GetMetersAsync(ct);

        if (!extractionResult.TryGetSuccessValue(out var readings))
        {
            metrics.RecordFetch(FetchOutcome.Failed);

            logger.FailedToExtractReadings(extractionResult.ErrorMessage);

            return extractionResult;
        }

        if (readings.Count == 0)
        {
            metrics.RecordFetch(FetchOutcome.Empty);

            logger.NoReadingsToPublish();

            return Result.Succeed();
        }

        metrics.RecordFetch(FetchOutcome.Succeeded);
        metrics.RecordReadingsReceived(readings);

        var publishingResult = await readingsPublisher.PublishAsync(readings, ct);

        if (publishingResult.IsFailure)
        {
            logger.FailedToPublishReadings(publishingResult.ErrorMessage);

            return publishingResult;
        }

        return Result.Succeed();
    }
}

internal static partial class ExternalMetersServiceLogs
{
    [LoggerMessage(
        Level = LogLevel.Error,
        Message = "Failed to extract readings. Reason: {Reason}")]
    public static partial void FailedToExtractReadings(this ILogger logger, string reason);

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "No readings were received, nothing to publish")]
    public static partial void NoReadingsToPublish(this ILogger logger);

    [LoggerMessage(
        Level = LogLevel.Error,
        Message = "Failed to publish readings. Reason: {Reason}")]
    public static partial void FailedToPublishReadings(this ILogger logger, string reason);
}
