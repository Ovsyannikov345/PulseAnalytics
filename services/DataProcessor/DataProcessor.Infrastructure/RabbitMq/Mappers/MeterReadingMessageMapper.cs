using DataIngestor.Contracts.Events;
using DataProcessor.Application.Readings.Models;

namespace DataProcessor.Infrastructure.RabbitMq.Mappers;

internal static class MeterReadingMessageMapper
{
    public static MotionReadingModel ToModel(MotionReadingReceived message)
    {
        return new MotionReadingModel
        {
            DeduplicationKey = message.MessageId,
            Location = message.Location,
            ReceivedAt = message.ReceivedAt,
            MotionDetected = message.MotionDetected
        };
    }

    public static EnergyReadingModel ToModel(EnergyReadingReceived message)
    {
        return new EnergyReadingModel
        {
            DeduplicationKey = message.MessageId,
            Location = message.Location,
            ReceivedAt = message.ReceivedAt,
            Energy = message.Energy
        };
    }

    public static AirQualityReadingModel ToModel(AirQualityReadingReceived message)
    {
        return new AirQualityReadingModel
        {
            DeduplicationKey = message.MessageId,
            Location = message.Location,
            ReceivedAt = message.ReceivedAt,
            Co2 = message.Co2,
            Pm25 = message.Pm25,
            Humidity = message.Humidity
        };
    }
}
