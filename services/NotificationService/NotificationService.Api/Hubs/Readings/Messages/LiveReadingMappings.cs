using System.Diagnostics;
using NotificationService.Application.LiveReadings.Models;

namespace NotificationService.Api.Hubs.Readings.Messages;

internal static class LiveReadingMappings
{
    public static LiveReading ToMessage(this LiveReadingModel reading)
    {
        return reading switch
        {
            MotionLiveReadingModel motion => new MotionLiveReading
            {
                Id = motion.Id,
                Location = motion.Location,
                ReceivedAt = motion.ReceivedAt,
                MotionDetected = motion.MotionDetected
            },
            EnergyLiveReadingModel energy => new EnergyLiveReading
            {
                Id = energy.Id,
                Location = energy.Location,
                ReceivedAt = energy.ReceivedAt,
                Energy = energy.Energy
            },
            AirQualityLiveReadingModel airQuality => new AirQualityLiveReading
            {
                Id = airQuality.Id,
                Location = airQuality.Location,
                ReceivedAt = airQuality.ReceivedAt,
                Co2 = airQuality.Co2,
                Pm25 = airQuality.Pm25,
                Humidity = airQuality.Humidity
            },
            _ => throw new UnreachableException($"No hub message for {reading.GetType().Name}")
        };
    }
}
