using System.ComponentModel.DataAnnotations;

namespace DataIngestor.Background.Configuration;

internal sealed record MeterPollingWorkerSettings
{
    public const string SectionName = "MeterPollingWorker";

    [Range(1, 3600)]
    public required int PollingPeriodInSeconds { get; init; }

    [Range(1, 300)]
    public int? StartupDelayInSeconds { get; init; }
}
