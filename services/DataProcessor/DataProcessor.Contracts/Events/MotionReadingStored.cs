namespace DataProcessor.Contracts.Events;

/// <summary>
/// Published when a motion meter reading is stored.
/// </summary>
public sealed record MotionReadingStored : MeterReadingStoredEvent
{
    /// <summary>
    /// Gets a value indicating whether the meter detected motion.
    /// </summary>
    public required bool MotionDetected { get; init; }
}
