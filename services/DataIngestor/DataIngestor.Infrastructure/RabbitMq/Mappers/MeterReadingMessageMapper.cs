using System.Diagnostics;
using DataIngestor.Contracts.Events;
using DataIngestor.Domain.Entities;
using DataIngestor.Infrastructure.Abstractions;
using DataIngestor.Infrastructure.RabbitMq.Topology;

namespace DataIngestor.Infrastructure.RabbitMq.Mappers;

internal static class MeterReadingMessageMapper
{
    public static OutgoingMessage ToOutgoingMessage(MeterReading reading)
    {
        // Time-ordered identifier, so messages sort by the time their readings were received.
        var messageId = Guid.CreateVersion7(reading.ReceivedAt);

        // The event type and its routing key are chosen in the same arm, so they cannot get out of sync.
        return reading.Payload switch
        {
            MotionPayload motion => CreateMessage(
                new MotionReadingReceived
                {
                    MessageId = messageId,
                    Location = reading.Name,
                    ReceivedAt = reading.ReceivedAt,
                    MotionDetected = motion.MotionDetected
                },
                MeterReadingsTopology.RoutingKeys.MotionReadingReceived),

            EnergyPayload energy => CreateMessage(
                new EnergyReadingReceived
                {
                    MessageId = messageId,
                    Location = reading.Name,
                    ReceivedAt = reading.ReceivedAt,
                    Energy = energy.Energy
                },
                MeterReadingsTopology.RoutingKeys.EnergyReadingReceived),

            AirQualityPayload airQuality => CreateMessage(
                new AirQualityReadingReceived
                {
                    MessageId = messageId,
                    Location = reading.Name,
                    ReceivedAt = reading.ReceivedAt,
                    Co2 = airQuality.Co2,
                    Pm25 = airQuality.Pm25,
                    Humidity = airQuality.Humidity
                },
                MeterReadingsTopology.RoutingKeys.AirQualityReadingReceived),

            _ => throw new UnreachableException($"No message mapping for reading payload type {reading.Payload.GetType().Name}")
        };
    }

    private static OutgoingMessage CreateMessage(MeterReadingEvent body, string routingKey)
    {
        return new OutgoingMessage(MeterReadingsTopology.ExchangeName, routingKey, body.MessageId, body);
    }
}
