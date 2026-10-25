using DataIngestor.Domain.Constants.Enums;
using DataIngestor.Domain.Entities;

namespace DataIngestor.UnitTests.Utilities;

internal static class MeterReadings
{
    public static readonly DateTimeOffset ReceivedAt = new(2026, 10, 24, 12, 0, 0, TimeSpan.Zero);

    public static MeterReading Motion(string name = "Garage", bool motionDetected = true)
    {
        return new MeterReading
        {
            Name = name,
            Type = MeterType.Motion,
            ReceivedAt = ReceivedAt,
            Payload = new MotionPayload { MotionDetected = motionDetected }
        };
    }

    public static MeterReading Energy(string name = "Office", double energy = 845.62)
    {
        return new MeterReading
        {
            Name = name,
            Type = MeterType.Energy,
            ReceivedAt = ReceivedAt,
            Payload = new EnergyPayload { Energy = energy }
        };
    }

    public static MeterReading AirQuality(string name = "Kitchen", int co2 = 803, int pm25 = 25, int humidity = 36)
    {
        return new MeterReading
        {
            Name = name,
            Type = MeterType.AirQuality,
            ReceivedAt = ReceivedAt,
            Payload = new AirQualityPayload { Co2 = co2, Pm25 = pm25, Humidity = humidity }
        };
    }
}
