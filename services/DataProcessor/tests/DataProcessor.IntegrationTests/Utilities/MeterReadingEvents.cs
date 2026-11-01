using DataIngestor.Contracts.Events;

namespace DataProcessor.IntegrationTests.Utilities;

/// <summary>
/// Events as the ingestor publishes them. Every call gets a new message ID, so tests do not see each other's readings.
/// </summary>
internal static class MeterReadingEvents
{
    // PostgreSQL timestamptz columns accept UTC values only, and the ingestor publishes UTC.
    public static readonly DateTimeOffset ReceivedAt = new(2026, 11, 1, 12, 0, 0, TimeSpan.Zero);

    public static MotionReadingReceived Motion()
    {
        return new MotionReadingReceived
        {
            MessageId = Guid.CreateVersion7(),
            Location = "Garage",
            ReceivedAt = ReceivedAt,
            MotionDetected = true
        };
    }

    public static EnergyReadingReceived Energy()
    {
        return new EnergyReadingReceived
        {
            MessageId = Guid.CreateVersion7(),
            Location = "Office",
            ReceivedAt = ReceivedAt,
            Energy = 845.62
        };
    }

    public static AirQualityReadingReceived AirQuality()
    {
        return new AirQualityReadingReceived
        {
            MessageId = Guid.CreateVersion7(),
            Location = "Living Room",
            ReceivedAt = ReceivedAt,
            Co2 = 803,
            Pm25 = 25,
            Humidity = 36
        };
    }
}
