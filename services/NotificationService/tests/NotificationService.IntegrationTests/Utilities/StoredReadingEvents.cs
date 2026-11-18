using DataProcessor.Contracts.Enums;
using DataProcessor.Contracts.Events;

namespace NotificationService.IntegrationTests.Utilities;

/// <summary>
/// Events as the processor publishes them. Every call gets new message and reading IDs, so tests do not see
/// each other's readings.
/// </summary>
internal static class StoredReadingEvents
{
    public static readonly DateTimeOffset ReceivedAt = new(2026, 11, 17, 10, 15, 30, 123, TimeSpan.Zero);

    public static MotionReadingStored Motion()
    {
        return new MotionReadingStored
        {
            MessageId = Guid.CreateVersion7(),
            ReadingId = Guid.CreateVersion7(),
            Location = MeterLocation.Garage,
            ReceivedAt = ReceivedAt,
            MotionDetected = true
        };
    }

    public static EnergyReadingStored Energy()
    {
        return new EnergyReadingStored
        {
            MessageId = Guid.CreateVersion7(),
            ReadingId = Guid.CreateVersion7(),
            Location = MeterLocation.Office,
            ReceivedAt = ReceivedAt,
            Energy = 845.62
        };
    }

    public static AirQualityReadingStored AirQuality()
    {
        return new AirQualityReadingStored
        {
            MessageId = Guid.CreateVersion7(),
            ReadingId = Guid.CreateVersion7(),
            Location = MeterLocation.LivingRoom,
            ReceivedAt = ReceivedAt,
            Co2 = 803,
            Pm25 = 25,
            Humidity = 36
        };
    }
}
