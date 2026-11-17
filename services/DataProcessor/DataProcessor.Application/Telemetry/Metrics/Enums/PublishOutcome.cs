namespace DataProcessor.Application.Telemetry.Metrics.Enums;

public enum PublishOutcome
{
    Confirmed = 1,
    TimedOut = 2,
    Nacked = 3,
    Failed = 4,
    Skipped = 5
}
