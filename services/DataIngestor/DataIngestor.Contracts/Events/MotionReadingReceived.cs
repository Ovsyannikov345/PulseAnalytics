namespace DataIngestor.Contracts.Events;

/// <summary>
/// Published when a motion meter reading is received.
/// </summary>
public sealed record MotionReadingReceived : MeterReadingEvent
{
    /// <summary>
    /// Gets a value indicating whether the meter detected motion.
    /// </summary>
    public required bool MotionDetected { get; init; }
}
