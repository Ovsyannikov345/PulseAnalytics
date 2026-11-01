using DataIngestor.Contracts.Events;
using DataProcessor.Application.Readings.Models;
using DataProcessor.Infrastructure.RabbitMq.Mappers;
using Shouldly;
using Xunit;

namespace DataProcessor.UnitTests.Infrastructure.RabbitMq.Mappers;

public sealed class MeterReadingMessageMapperTests
{
    private static readonly Guid MessageId = Guid.Parse("0199f5c4-3a1e-7b52-9d0e-5c8a7e6b4f21");

    private static readonly DateTimeOffset ReceivedAt = new(2026, 11, 1, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ToModel_MotionReadingReceived_MapsMessageIdToDeduplicationKeyAndCopiesValues()
    {
        // Arrange
        var message = new MotionReadingReceived
        {
            MessageId = MessageId,
            Location = "Garage",
            ReceivedAt = ReceivedAt,
            MotionDetected = true
        };

        // Act
        var model = MeterReadingMessageMapper.ToModel(message);

        // Assert
        model.ShouldBe(new MotionReadingModel
        {
            DeduplicationKey = MessageId,
            Location = "Garage",
            ReceivedAt = ReceivedAt,
            MotionDetected = true
        });
    }

    [Fact]
    public void ToModel_EnergyReadingReceived_MapsMessageIdToDeduplicationKeyAndCopiesValues()
    {
        // Arrange
        var message = new EnergyReadingReceived
        {
            MessageId = MessageId,
            Location = "Office",
            ReceivedAt = ReceivedAt,
            Energy = 845.62
        };

        // Act
        var model = MeterReadingMessageMapper.ToModel(message);

        // Assert
        model.ShouldBe(new EnergyReadingModel
        {
            DeduplicationKey = MessageId,
            Location = "Office",
            ReceivedAt = ReceivedAt,
            Energy = 845.62
        });
    }

    [Fact]
    public void ToModel_AirQualityReadingReceived_MapsMessageIdToDeduplicationKeyAndCopiesValues()
    {
        // Arrange
        var message = new AirQualityReadingReceived
        {
            MessageId = MessageId,
            Location = "Kitchen",
            ReceivedAt = ReceivedAt,
            Co2 = 803,
            Pm25 = 25,
            Humidity = 36
        };

        // Act
        var model = MeterReadingMessageMapper.ToModel(message);

        // Assert
        model.ShouldBe(new AirQualityReadingModel
        {
            DeduplicationKey = MessageId,
            Location = "Kitchen",
            ReceivedAt = ReceivedAt,
            Co2 = 803,
            Pm25 = 25,
            Humidity = 36
        });
    }
}
