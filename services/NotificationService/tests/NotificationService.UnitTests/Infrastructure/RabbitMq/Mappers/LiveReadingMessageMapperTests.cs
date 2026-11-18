using DataProcessor.Contracts.Events;
using NotificationService.Application.LiveReadings.Enums;
using NotificationService.Application.LiveReadings.Models;
using NotificationService.Infrastructure.RabbitMq.Mappers;
using Shouldly;
using Xunit;
using ContractMeterLocation = DataProcessor.Contracts.Enums.MeterLocation;

namespace NotificationService.UnitTests.Infrastructure.RabbitMq.Mappers;

public sealed class LiveReadingMessageMapperTests
{
    private static readonly Guid MessageId = Guid.Parse("019a91b2-4c6e-7d13-9a58-3e7f2b6c1e01");

    private static readonly Guid ReadingId = Guid.Parse("019a91b2-4c6e-7d13-9a58-3e7f2b6c1d01");

    private static readonly DateTimeOffset ReceivedAt = new(2026, 11, 17, 10, 15, 30, TimeSpan.Zero);

    [Fact]
    public void ToModel_MotionReadingStored_MapsReadingIdToIdAndCopiesValues()
    {
        // Arrange
        var message = new MotionReadingStored
        {
            MessageId = MessageId,
            ReadingId = ReadingId,
            Location = ContractMeterLocation.Garage,
            ReceivedAt = ReceivedAt,
            MotionDetected = true
        };

        // Act
        var result = LiveReadingMessageMapper.ToModel(message);

        // Assert
        result.TryGetSuccessValue(out var model).ShouldBeTrue();
        model.ShouldBe(new MotionLiveReadingModel
        {
            Id = ReadingId,
            Location = MeterLocation.Garage,
            ReceivedAt = ReceivedAt,
            MotionDetected = true
        });
    }

    [Fact]
    public void ToModel_EnergyReadingStored_MapsReadingIdToIdAndCopiesValues()
    {
        // Arrange
        var message = new EnergyReadingStored
        {
            MessageId = MessageId,
            ReadingId = ReadingId,
            Location = ContractMeterLocation.Office,
            ReceivedAt = ReceivedAt,
            Energy = 845.62
        };

        // Act
        var result = LiveReadingMessageMapper.ToModel(message);

        // Assert
        result.TryGetSuccessValue(out var model).ShouldBeTrue();
        model.ShouldBe(new EnergyLiveReadingModel
        {
            Id = ReadingId,
            Location = MeterLocation.Office,
            ReceivedAt = ReceivedAt,
            Energy = 845.62
        });
    }

    [Fact]
    public void ToModel_AirQualityReadingStored_MapsReadingIdToIdAndCopiesValues()
    {
        // Arrange
        var message = new AirQualityReadingStored
        {
            MessageId = MessageId,
            ReadingId = ReadingId,
            Location = ContractMeterLocation.LivingRoom,
            ReceivedAt = ReceivedAt,
            Co2 = 803,
            Pm25 = 25,
            Humidity = 36
        };

        // Act
        var result = LiveReadingMessageMapper.ToModel(message);

        // Assert
        result.TryGetSuccessValue(out var model).ShouldBeTrue();
        model.ShouldBe(new AirQualityLiveReadingModel
        {
            Id = ReadingId,
            Location = MeterLocation.LivingRoom,
            ReceivedAt = ReceivedAt,
            Co2 = 803,
            Pm25 = 25,
            Humidity = 36
        });
    }

    [Theory]
    [InlineData(ContractMeterLocation.Kitchen, MeterLocation.Kitchen)]
    [InlineData(ContractMeterLocation.LivingRoom, MeterLocation.LivingRoom)]
    [InlineData(ContractMeterLocation.Bedroom, MeterLocation.Bedroom)]
    [InlineData(ContractMeterLocation.Garage, MeterLocation.Garage)]
    [InlineData(ContractMeterLocation.Office, MeterLocation.Office)]
    [InlineData(ContractMeterLocation.Corridor, MeterLocation.Corridor)]
    public void ToModel_KnownLocation_MapsItToTheSameLocation(ContractMeterLocation location, MeterLocation expected)
    {
        // Arrange
        var message = CreateEnergyMessage(location);

        // Act
        var result = LiveReadingMessageMapper.ToModel(message);

        // Assert
        result.TryGetSuccessValue(out var model).ShouldBeTrue();
        model.Location.ShouldBe(expected);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(99)]
    public void ToModel_UndefinedLocation_Fails(int location)
    {
        // Arrange
        var message = CreateEnergyMessage((ContractMeterLocation)location);

        // Act
        var result = LiveReadingMessageMapper.ToModel(message);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.ErrorMessage.ShouldBe($"Unknown location '{location}'");
    }

    private static EnergyReadingStored CreateEnergyMessage(ContractMeterLocation location)
    {
        return new EnergyReadingStored
        {
            MessageId = MessageId,
            ReadingId = ReadingId,
            Location = location,
            ReceivedAt = ReceivedAt,
            Energy = 845.62
        };
    }
}
