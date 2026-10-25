using System.Diagnostics;
using DataIngestor.Contracts.Events;
using DataIngestor.Domain.Entities;
using DataIngestor.Infrastructure.RabbitMq.Mappers;
using DataIngestor.UnitTests.Utilities;
using Shouldly;
using Xunit;

namespace DataIngestor.UnitTests.Infrastructure.RabbitMq.Mappers;

public sealed class MeterReadingMessageMapperTests
{
    private const string ExchangeName = "pulse.meter-readings";

    [Fact]
    public void ToOutgoingMessage_MotionReading_MapsToMotionReadingReceived()
    {
        // Arrange
        var reading = MeterReadings.Motion("Garage", motionDetected: true);

        // Act
        var message = MeterReadingMessageMapper.ToOutgoingMessage(reading);

        // Assert
        message.Exchange.ShouldBe(ExchangeName);
        message.RoutingKey.ShouldBe("meter-reading.motion.received");
        message.Body.ShouldBe(new MotionReadingReceived
        {
            MessageId = message.MessageId,
            Location = "Garage",
            ReceivedAt = MeterReadings.ReceivedAt,
            MotionDetected = true
        });
    }

    [Fact]
    public void ToOutgoingMessage_EnergyReading_MapsToEnergyReadingReceived()
    {
        // Arrange
        var reading = MeterReadings.Energy("Office", energy: 845.62);

        // Act
        var message = MeterReadingMessageMapper.ToOutgoingMessage(reading);

        // Assert
        message.Exchange.ShouldBe(ExchangeName);
        message.RoutingKey.ShouldBe("meter-reading.energy.received");
        message.Body.ShouldBe(new EnergyReadingReceived
        {
            MessageId = message.MessageId,
            Location = "Office",
            ReceivedAt = MeterReadings.ReceivedAt,
            Energy = 845.62
        });
    }

    [Fact]
    public void ToOutgoingMessage_AirQualityReading_MapsToAirQualityReadingReceived()
    {
        // Arrange
        var reading = MeterReadings.AirQuality("Kitchen", co2: 803, pm25: 25, humidity: 36);

        // Act
        var message = MeterReadingMessageMapper.ToOutgoingMessage(reading);

        // Assert
        message.Exchange.ShouldBe(ExchangeName);
        message.RoutingKey.ShouldBe("meter-reading.air-quality.received");
        message.Body.ShouldBe(new AirQualityReadingReceived
        {
            MessageId = message.MessageId,
            Location = "Kitchen",
            ReceivedAt = MeterReadings.ReceivedAt,
            Co2 = 803,
            Pm25 = 25,
            Humidity = 36
        });
    }

    [Fact]
    public void ToOutgoingMessage_AnyReading_UsesVersion7MessageIdBasedOnReceiveTime()
    {
        // Arrange
        var reading = MeterReadings.Motion();

        // Act
        var message = MeterReadingMessageMapper.ToOutgoingMessage(reading);

        // Assert
        message.MessageId.Version.ShouldBe(7);
        // The first 48 bits of a version 7 GUID hold its Unix timestamp in milliseconds.
        var timestampMs = Convert.ToInt64(message.MessageId.ToString("N")[..12], 16);
        timestampMs.ShouldBe(MeterReadings.ReceivedAt.ToUnixTimeMilliseconds());
    }

    [Fact]
    public void ToOutgoingMessage_SameReadingMappedTwice_GeneratesDifferentMessageIds()
    {
        // Arrange
        var reading = MeterReadings.Energy();

        // Act
        var first = MeterReadingMessageMapper.ToOutgoingMessage(reading);
        var second = MeterReadingMessageMapper.ToOutgoingMessage(reading);

        // Assert
        first.MessageId.ShouldNotBe(second.MessageId);
    }

    [Theory]
    [MemberData(nameof(ReadingsWithEventNames))]
    public void ToOutgoingMessage_AnyReading_ExposesEventNameAsMessageType(MeterReading reading, string eventName)
    {
        // Act
        var message = MeterReadingMessageMapper.ToOutgoingMessage(reading);

        // Assert
        message.MessageType.ShouldBe(eventName);
    }

    [Fact]
    public void ToOutgoingMessage_UnmappedPayloadType_ThrowsUnreachableException()
    {
        // Arrange
        var reading = MeterReadings.Motion() with { Payload = new UnmappedPayload() };

        // Act
        var act = () => MeterReadingMessageMapper.ToOutgoingMessage(reading);

        // Assert
        act.ShouldThrow<UnreachableException>();
    }

    public static TheoryData<MeterReading, string> ReadingsWithEventNames()
    {
        return new()
        {
            { MeterReadings.Motion(), nameof(MotionReadingReceived) },
            { MeterReadings.Energy(), nameof(EnergyReadingReceived) },
            { MeterReadings.AirQuality(), nameof(AirQualityReadingReceived) }
        };
    }

    private sealed record UnmappedPayload : ReadingPayload;
}
