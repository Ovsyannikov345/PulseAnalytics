using ApiGateway.Common.Constants.Enums;
using ApiGateway.DataAccess.Entities;

namespace ApiGateway.UnitTests.Utilities;

/// <summary>
/// Rows of the <c>reporting.meter_readings</c> view as the gateway reads them: only the reading type's own columns are set.
/// </summary>
internal static class StoredReadings
{
    public static MeterReading Motion(MeterLocation location, DateTimeOffset receivedAt, bool motionDetected = true, Guid? id = null)
    {
        return new MeterReading
        {
            Id = id ?? Guid.CreateVersion7(),
            Type = MeterType.Motion,
            Location = location,
            ReceivedAt = receivedAt,
            MotionDetected = motionDetected
        };
    }

    public static MeterReading Energy(MeterLocation location, DateTimeOffset receivedAt, double energy = 1.5, Guid? id = null)
    {
        return new MeterReading
        {
            Id = id ?? Guid.CreateVersion7(),
            Type = MeterType.Energy,
            Location = location,
            ReceivedAt = receivedAt,
            Energy = energy
        };
    }

    public static MeterReading AirQuality(
        MeterLocation location,
        DateTimeOffset receivedAt,
        int co2 = 600,
        int pm25 = 12,
        int humidity = 45,
        Guid? id = null)
    {
        return new MeterReading
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
