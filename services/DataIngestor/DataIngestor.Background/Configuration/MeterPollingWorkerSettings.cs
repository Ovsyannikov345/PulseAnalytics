using System.ComponentModel.DataAnnotations;

namespace DataIngestor.Background.Configuration;

internal sealed record MeterPollingWorkerSettings
{
    public const string SectionName = "MeterPollingWorker";

    [Range(1, int.MaxValue)]
    public required int PollingPeriodInSeconds { get; init; }

    [Range(1, int.MaxValue)]
    public int? StartupDelayInSeconds { get; init; }
}
