using DataIngestor.Contracts.Events;
using DataProcessor.Application.MeterReadings.Models;
using DataProcessor.Domain.Constants.Enums;
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
        var result = MeterReadingMessageMapper.ToModel(message);

        // Assert
        result.TryGetSuccessValue(out var model).ShouldBeTrue();
        model.ShouldBe(new MotionReadingModel
        {
            DeduplicationKey = MessageId,
            Location = MeterLocation.Garage,
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
        var result = MeterReadingMessageMapper.ToModel(message);

        // Assert
        result.TryGetSuccessValue(out var model).ShouldBeTrue();
        model.ShouldBe(new EnergyReadingModel
        {
            DeduplicationKey = MessageId,
            Location = MeterLocation.Office,
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
        var result = MeterReadingMessageMapper.ToModel(message);

        // Assert
        result.TryGetSuccessValue(out var model).ShouldBeTrue();
        model.ShouldBe(new AirQualityReadingModel
        {
            DeduplicationKey = MessageId,
            Location = MeterLocation.Kitchen,
            ReceivedAt = ReceivedAt,
            Co2 = 803,
            Pm25 = 25,
            Humidity = 36
        });
    }

    [Theory]
    [InlineData("Kitchen", MeterLocation.Kitchen)]
    [InlineData("Living Room", MeterLocation.LivingRoom)]
    [InlineData("Bedroom", MeterLocation.Bedroom)]
    [InlineData("Garage", MeterLocation.Garage)]
    [InlineData("Office", MeterLocation.Office)]
    [InlineData("Corridor", MeterLocation.Corridor)]
    [InlineData("living room", MeterLocation.LivingRoom)]
    public void ToModel_KnownLocationName_MapsItToLocation(string locationName, MeterLocation expectedLocation)
    {
        // Act
        var result = MeterReadingMessageMapper.ToModel(CreateEnergyMessage(locationName));

        // Assert
        result.TryGetSuccessValue(out var model).ShouldBeTrue();
        model.Location.ShouldBe(expectedLocation);
    }

    [Theory]
    [InlineData("Attic")]
    [InlineData("LivingRoom")]
    [InlineData("")]
    public void ToModel_UnknownLocationName_Fails(string locationName)
    {
        // Act
        var result = MeterReadingMessageMapper.ToModel(CreateEnergyMessage(locationName));

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.ErrorMessage.ShouldBe($"Unknown location '{locationName}'");
    }

    private static EnergyReadingReceived CreateEnergyMessage(string location)
    {
        return new EnergyReadingReceived
        {
            MessageId = MessageId,
            Location = location,
            ReceivedAt = ReceivedAt,
            Energy = 845.62
        };
    }
}
