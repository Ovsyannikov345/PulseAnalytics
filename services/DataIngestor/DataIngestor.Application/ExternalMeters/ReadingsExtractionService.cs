using Ausiannikau.Utilities.Results;
using DataIngestor.Application.Interfaces.Clients;
using Microsoft.Extensions.Logging;

namespace DataIngestor.Application.ExternalMeters;

public interface IReadingsExtractionService
{
    public Task<Result> ExtractAsync(CancellationToken ct);
}

internal sealed class ReadingsExtractionService(IMetersClient metersClient, ILogger<ReadingsExtractionService> logger)
    : IReadingsExtractionService
{
    public async Task<Result> ExtractAsync(CancellationToken ct)
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
