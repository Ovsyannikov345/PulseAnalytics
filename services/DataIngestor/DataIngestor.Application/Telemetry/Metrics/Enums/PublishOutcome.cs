namespace DataIngestor.Application.Telemetry.Metrics.Enums;

public enum PublishOutcome
{
    Confirmed = 1,
    TimedOut = 2,
    Returned = 3,
    Nacked = 4,
    Failed = 5,
    Skipped = 6
}
