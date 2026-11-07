using ApiGateway.Common.Constants.Enums;

namespace ApiGateway.IntegrationTests.Utilities;

/// <summary>
/// Readings to store before a test: only the reading type's own values are set, as the processor stores them.
/// </summary>
internal static class SeedReadings
{
    public static SeedReading Motion(MeterLocation location, DateTimeOffset receivedAt, bool motionDetected = true, Guid? id = null)
    {
        return new SeedReading
        {
            Id = id ?? Guid.CreateVersion7(),
            Type = MeterType.Motion,
            Location = location,
            ReceivedAt = receivedAt,
            MotionDetected = motionDetected
        };
    }

    public static SeedReading Energy(MeterLocation location, DateTimeOffset receivedAt, double energy = 1.5, Guid? id = null)
    {
        return new SeedReading
        {
            Id = id ?? Guid.CreateVersion7(),
            Type = MeterType.Energy,
            Location = location,
            ReceivedAt = receivedAt,
            Energy = energy
        };
    }

    public static SeedReading AirQuality(
        MeterLocation location,
        DateTimeOffset receivedAt,
        int co2 = 600,
        int pm25 = 12,
        int humidity = 45,
        Guid? id = null)
    {
        return new SeedReading
        {
            Id = id ?? Guid.CreateVersion7(),
            Type = MeterType.AirQuality,
            Location = location,
            ReceivedAt = receivedAt,
            Co2 = co2,
            Pm25 = pm25,
            Humidity = humidity
        };
    }
}
