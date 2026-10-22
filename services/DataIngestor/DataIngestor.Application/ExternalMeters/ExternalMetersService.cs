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
            // TODO log

            return extractionResult;
        }

        foreach (var reading in readings)
        {
            // TODO publish readings

            logger.LogInformation($"Publishing reading:\n{reading}\n");
        }

        return Result.Succeed();
    }
}
