namespace DataIngestor.Contracts.Events;

/// <summary>
/// Published when an energy meter reading is received.
/// </summary>
public sealed record EnergyReadingReceived : MeterReadingEvent
{
    /// <summary>
    /// Gets the energy value reported by the meter.
    /// </summary>
    public required double Energy { get; init; }
}
