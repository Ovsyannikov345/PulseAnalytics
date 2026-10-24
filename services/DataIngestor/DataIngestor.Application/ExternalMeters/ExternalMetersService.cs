using Ausiannikau.Utilities.Results;
using DataIngestor.Application.Interfaces.Clients;
using Microsoft.Extensions.Logging;

namespace DataIngestor.Application.ExternalMeters;

public interface IExternalMetersService
{
    public Task<Result> ExtractReadingsAsync(CancellationToken ct);
}

internal sealed class ExternalMetersService(
    IExternalMetersClient metersClient,
    ILogger<ExternalMetersService> logger)
    : IExternalMetersService
{
    public async Task<Result> ExtractReadingsAsync(CancellationToken ct)
    {
        var extractionResult = await metersClient.GetMetersAsync(ct);

        if (!extractionResult.TryGetSuccessValue(out var readings))
        {
            logger.FailedToExtractReadings(extractionResult.ErrorMessage);

            return extractionResult;
        }

        foreach (var reading in readings)
        {
            // TODO replace with real publishing logic
            logger.LogInformation("Publishing reading: {Reading}", reading.ToString());
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
}