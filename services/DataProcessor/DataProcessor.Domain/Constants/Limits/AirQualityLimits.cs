namespace DataProcessor.Domain.Constants.Limits;

public static class AirQualityLimits
{
    /// <summary>
    /// Lowest CO2 concentration, in ppm.
    /// </summary>
    public const int MinCo2 = 0;

    /// <summary>
    /// Highest CO2 concentration, in ppm.
    /// </summary>
    public const int MaxCo2 = 10_000;

    /// <summary>
    /// Lowest PM2.5 concentration, in µg/m³.
    /// </summary>
    public const int MinPm25 = 0;

    /// <summary>
    /// Highest PM2.5 concentration, in µg/m³.
    /// </summary>
    public const int MaxPm25 = 1_000;

    /// <summary>
    /// Lowest relative humidity, in %.
    /// </summary>
    public const int MinHumidity = 0;

    /// <summary>
    /// Highest relative humidity, in %.
    /// </summary>
    public const int MaxHumidity = 100;
}
