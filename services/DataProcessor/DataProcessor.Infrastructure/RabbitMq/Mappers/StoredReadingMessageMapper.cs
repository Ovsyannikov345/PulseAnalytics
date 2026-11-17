using System.Diagnostics;
using DataProcessor.Contracts.Events;
using DataProcessor.Domain.Constants.Enums;
using DataProcessor.Domain.Entities.MeterReadings;
using DataProcessor.Domain.Entities.MeterReadings.Payloads;
using DataProcessor.Infrastructure.Abstractions;
using DataProcessor.Infrastructure.RabbitMq.Topology;
using ContractMeterLocation = DataProcessor.Contracts.Enums.MeterLocation;

namespace DataProcessor.Infrastructure.RabbitMq.Mappers;

internal static class StoredReadingMessageMapper
{
    public static string GetMessageType(MeterReading reading)
    {
        return reading.Payload switch
        {
            MotionPayload => nameof(MotionReadingStored),
            EnergyPayload => nameof(EnergyReadingStored),
            AirQualityPayload => nameof(AirQualityReadingStored),

            _ => throw new UnreachableException(
                $"No message type for reading payload type {reading.Payload.GetType().Name}")
        };
    }

    public static OutgoingMessage ToOutgoingMessage(MeterReading reading)
    {
        var messageId = Guid.CreateVersion7();
        var location = ToExtenrnalContractLocation(reading.Location);

        return reading.Payload switch
        {
            MotionPayload motion => CreateMessage(
                new MotionReadingStored
                {
                    MessageId = messageId,
                    ReadingId = reading.Id,
                    Location = location,
                    ReceivedAt = reading.ReceivedAt,
                    MotionDetected = motion.MotionDetected
                },
                RabbitMqTopology.Exchanges.StoredReadingsExchange.RoutingKeys.MotionReadingStored),

            EnergyPayload energy => CreateMessage(
                new EnergyReadingStored
                {
                    MessageId = messageId,
                    ReadingId = reading.Id,
                    Location = location,
                    ReceivedAt = reading.ReceivedAt,
                    Energy = energy.Energy
                },
                RabbitMqTopology.Exchanges.StoredReadingsExchange.RoutingKeys.EnergyReadingStored),

            AirQualityPayload airQuality => CreateMessage(
                new AirQualityReadingStored
                {
                    MessageId = messageId,
                    ReadingId = reading.Id,
                    Location = location,
                    ReceivedAt = reading.ReceivedAt,
                    Co2 = airQuality.Co2,
                    Pm25 = airQuality.Pm25,
                    Humidity = airQuality.Humidity
                },
                RabbitMqTopology.Exchanges.StoredReadingsExchange.RoutingKeys.AirQualityReadingStored),

            _ => throw new UnreachableException(
                $"No message mapping for reading payload type {reading.Payload.GetType().Name}")
        };
    }

    // Stored events are live hints: the reading is already in the database and the meter is read again
    // 15 s later, so they are neither persisted by the broker nor mandatory (dropped while no queue is bound).
    private static OutgoingMessage CreateMessage(MeterReadingStoredEvent body, string routingKey)
    {
        return new OutgoingMessage(
            RabbitMqTopology.Exchanges.StoredReadingsExchange.Name,
            routingKey,
            body.MessageId,
            body,
            Mandatory: false,
            Persistent: false);
    }

    private static ContractMeterLocation ToExtenrnalContractLocation(MeterLocation location)
    {
        return location switch
        {
            MeterLocation.Kitchen => ContractMeterLocation.Kitchen,
            MeterLocation.LivingRoom => ContractMeterLocation.LivingRoom,
            MeterLocation.Bedroom => ContractMeterLocation.Bedroom,
            MeterLocation.Garage => ContractMeterLocation.Garage,
            MeterLocation.Office => ContractMeterLocation.Office,
            MeterLocation.Corridor => ContractMeterLocation.Corridor,
            _ => throw new UnreachableException($"No contract value for meter location {location}")
        };
    }
}
