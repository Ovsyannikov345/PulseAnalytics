using System.Diagnostics;
using DataProcessor.Contracts.Events;
using DataProcessor.Domain.Constants.Enums;
using DataProcessor.Domain.Entities.MeterReadings;
using DataProcessor.Domain.Entities.MeterReadings.Payloads;
using DataProcessor.Infrastructure.RabbitMq.Mappers;
using Shouldly;
using Xunit;
using ContractMeterLocation = DataProcessor.Contracts.Enums.MeterLocation;

namespace DataProcessor.UnitTests.Infrastructure.RabbitMq.Mappers;

public sealed class StoredReadingMessageMapperTests
{
    private const string ExchangeName = "pulse.meter-readings.stored";

    private static readonly Guid ReadingId = Guid.Parse("019a8f2e-5b90-7d21-8c4e-7f1a3b5d9e02");

    private static readonly DateTimeOffset ReceivedAt = new(2026, 11, 17, 10, 15, 30, TimeSpan.Zero);

    [Fact]
    public void ToOutgoingMessage_MotionReading_MapsToMotionReadingStored()
    {
        // Arrange
        var reading = CreateReading(MeterType.Motion, MeterLocation.Garage, new MotionPayload { MotionDetected = true });

        // Act
        var message = StoredReadingMessageMapper.ToOutgoingMessage(reading);

        // Assert
        message.Exchange.ShouldBe(ExchangeName);
        message.RoutingKey.ShouldBe("meter-reading.motion.stored");
        message.MessageType.ShouldBe(nameof(MotionReadingStored));
        message.Body.ShouldBe(new MotionReadingStored
        {
            MessageId = message.MessageId,
            ReadingId = ReadingId,
            Location = ContractMeterLocation.Garage,
            ReceivedAt = ReceivedAt,
            MotionDetected = true
        });
    }

    [Fact]
    public void ToOutgoingMessage_EnergyReading_MapsToEnergyReadingStored()
    {
        // Arrange
        var reading = CreateReading(MeterType.Energy, MeterLocation.Office, new EnergyPayload { Energy = 845.62 });

        // Act
        var message = StoredReadingMessageMapper.ToOutgoingMessage(reading);

        // Assert
        message.Exchange.ShouldBe(ExchangeName);
        message.RoutingKey.ShouldBe("meter-reading.energy.stored");
        message.MessageType.ShouldBe(nameof(EnergyReadingStored));
        message.Body.ShouldBe(new EnergyReadingStored
        {
            MessageId = message.MessageId,
            ReadingId = ReadingId,
            Location = ContractMeterLocation.Office,
            ReceivedAt = ReceivedAt,
            Energy = 845.62
        });
    }

    [Fact]
    public void ToOutgoingMessage_AirQualityReading_MapsToAirQualityReadingStored()
    {
        // Arrange
        var reading = CreateReading(
            MeterType.AirQuality,
            MeterLocation.LivingRoom,
            new AirQualityPayload { Co2 = 803, Pm25 = 25, Humidity = 36 });

        // Act
        var message = StoredReadingMessageMapper.ToOutgoingMessage(reading);

        // Assert
        message.Exchange.ShouldBe(ExchangeName);
        message.RoutingKey.ShouldBe("meter-reading.air-quality.stored");
        message.MessageType.ShouldBe(nameof(AirQualityReadingStored));
        message.Body.ShouldBe(new AirQualityReadingStored
        {
            MessageId = message.MessageId,
            ReadingId = ReadingId,
            Location = ContractMeterLocation.LivingRoom,
            ReceivedAt = ReceivedAt,
            Co2 = 803,
            Pm25 = 25,
            Humidity = 36
        });
    }

    [Theory]
    [InlineData(MeterLocation.Kitchen, ContractMeterLocation.Kitchen)]
    [InlineData(MeterLocation.LivingRoom, ContractMeterLocation.LivingRoom)]
    [InlineData(MeterLocation.Bedroom, ContractMeterLocation.Bedroom)]
    [InlineData(MeterLocation.Garage, ContractMeterLocation.Garage)]
    [InlineData(MeterLocation.Office, ContractMeterLocation.Office)]
    [InlineData(MeterLocation.Corridor, ContractMeterLocation.Corridor)]
    public void ToOutgoingMessage_AnyLocation_MapsToContractLocationOfSameName(
        MeterLocation location,
        ContractMeterLocation expectedLocation)
    {
        // Arrange
        var reading = CreateReading(MeterType.Motion, location, new MotionPayload { MotionDetected = false });

        // Act
        var message = StoredReadingMessageMapper.ToOutgoingMessage(reading);

        // Assert
        message.Body.ShouldBeOfType<MotionReadingStored>().Location.ShouldBe(expectedLocation);
    }

    [Fact]
    public void ToOutgoingMessage_AnyReading_CreatesTransientNonMandatoryMessage()
    {
        // Arrange
        var reading = CreateReading(MeterType.Energy, MeterLocation.Office, new EnergyPayload { Energy = 1 });

        // Act
        var message = StoredReadingMessageMapper.ToOutgoingMessage(reading);

        // Assert
        message.Mandatory.ShouldBeFalse();
        message.Persistent.ShouldBeFalse();
    }

    [Fact]
    public void ToOutgoingMessage_AnyReading_UsesNewVersion7MessageIdOfTheEvent()
    {
        // Arrange
        var reading = CreateReading(MeterType.Energy, MeterLocation.Office, new EnergyPayload { Energy = 1 });

        // Act
        var first = StoredReadingMessageMapper.ToOutgoingMessage(reading);
        var second = StoredReadingMessageMapper.ToOutgoingMessage(reading);

        // Assert
        first.MessageId.Version.ShouldBe(7);
        first.MessageId.ShouldNotBe(second.MessageId);
        first.MessageId.ShouldNotBe(ReadingId);
        first.Body.ShouldBeAssignableTo<MeterReadingStoredEvent>().ShouldNotBeNull().MessageId.ShouldBe(first.MessageId);
    }

    [Theory]
    [InlineData(MeterType.Motion, nameof(MotionReadingStored))]
    [InlineData(MeterType.Energy, nameof(EnergyReadingStored))]
    [InlineData(MeterType.AirQuality, nameof(AirQualityReadingStored))]
    public void GetMessageType_AnyReading_ReturnsNameOfItsEvent(MeterType meterType, string expectedMessageType)
    {
        // Arrange
        IReadingPayload payload = meterType switch
        {
            MeterType.Motion => new MotionPayload { MotionDetected = true },
            MeterType.Energy => new EnergyPayload { Energy = 1 },
            _ => new AirQualityPayload { Co2 = 400, Pm25 = 5, Humidity = 40 }
        };

        // Act
        var messageType = StoredReadingMessageMapper.GetMessageType(CreateReading(meterType, MeterLocation.Kitchen, payload));

        // Assert
        messageType.ShouldBe(expectedMessageType);
    }

    [Fact]
    public void ToOutgoingMessage_UnknownPayload_ThrowsUnreachableException()
    {
        // Arrange
        var reading = CreateReading(MeterType.Motion, MeterLocation.Kitchen, new UnknownPayload());

        // Act
        var act = () => StoredReadingMessageMapper.ToOutgoingMessage(reading);

        // Assert
        act.ShouldThrow<UnreachableException>();
    }

    [Fact]
    public void GetMessageType_UnknownPayload_ThrowsUnreachableException()
    {
        // Arrange
        var reading = CreateReading(MeterType.Motion, MeterLocation.Kitchen, new UnknownPayload());

        // Act
        var act = () => StoredReadingMessageMapper.GetMessageType(reading);

        // Assert
        act.ShouldThrow<UnreachableException>();
    }

    [Fact]
    public void ToOutgoingMessage_UndefinedLocation_ThrowsUnreachableException()
    {
        // Arrange
        var reading = CreateReading(MeterType.Motion, (MeterLocation)int.MaxValue, new MotionPayload { MotionDetected = true });

        // Act
        var act = () => StoredReadingMessageMapper.ToOutgoingMessage(reading);

        // Assert
        act.ShouldThrow<UnreachableException>();
    }

    private static MeterReading CreateReading(MeterType type, MeterLocation location, IReadingPayload payload)
    {
        return new MeterReading
        {
            Id = ReadingId,
            DeduplicationKey = Guid.CreateVersion7(),
            Type = type,
            Location = location,
            ReceivedAt = ReceivedAt,
            Payload = payload
        };
    }

    private sealed record UnknownPayload : IReadingPayload;
}
