namespace DataProcessor.Contracts.Events;

/// <summary>
/// Published when an energy meter reading is stored.
/// </summary>
public sealed record EnergyReadingStored : MeterReadingStoredEvent
{
    /// <summary>
    /// Gets the energy value reported by the meter.
    /// </summary>
    public required double Energy { get; init; }
}
