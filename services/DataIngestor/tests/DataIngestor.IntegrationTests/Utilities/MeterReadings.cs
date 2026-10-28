using DataIngestor.Domain.Constants.Enums;
using DataIngestor.Domain.Entities;

namespace DataIngestor.IntegrationTests.Utilities;

internal static class MeterReadings
{
    public static readonly DateTimeOffset ReceivedAt = new(2026, 10, 24, 12, 0, 0, TimeSpan.Zero);

    public static MeterReading Motion()
    {
        return new MeterReading
        {
            Name = "Garage",
            Type = MeterType.Motion,
            ReceivedAt = ReceivedAt,
            Payload = new MotionPayload { MotionDetected = true }
        };
    }

    public static MeterReading Energy()
    {
        return new MeterReading
        {
            Name = "Office",
            Type = MeterType.Energy,
            ReceivedAt = ReceivedAt,
            Payload = new EnergyPayload { Energy = 845.62 }
        };
    }

    public static MeterReading AirQuality()
    {
        return new MeterReading
        {
            Name = "Kitchen",
            Type = MeterType.AirQuality,
            ReceivedAt = ReceivedAt,
            Payload = new AirQualityPayload { Co2 = 803, Pm25 = 25, Humidity = 36 }
        };
    }
}
