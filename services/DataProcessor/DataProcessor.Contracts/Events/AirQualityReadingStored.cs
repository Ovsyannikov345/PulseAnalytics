namespace DataProcessor.Contracts.Events;

/// <summary>
/// Published when an air quality meter reading is stored.
/// </summary>
public sealed record AirQualityReadingStored : MeterReadingStoredEvent
{
    /// <summary>
    /// Gets the CO2 concentration.
    /// </summary>
    public required int Co2 { get; init; }

    /// <summary>
    /// Gets the PM2.5 particulate matter concentration.
    /// </summary>
    public required int Pm25 { get; init; }

    /// <summary>
    /// Gets the relative humidity.
    /// </summary>
    public required int Humidity { get; init; }
}
