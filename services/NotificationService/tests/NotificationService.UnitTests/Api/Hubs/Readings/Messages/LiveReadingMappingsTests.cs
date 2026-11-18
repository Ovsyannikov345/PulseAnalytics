using System.Diagnostics;
using NotificationService.Api.Hubs.Readings.Messages;
using NotificationService.Application.LiveReadings.Enums;
using NotificationService.Application.LiveReadings.Models;
using Shouldly;
using Xunit;

namespace NotificationService.UnitTests.Api.Hubs.Readings.Messages;

public sealed class LiveReadingMappingsTests
{
    private static readonly Guid ReadingId = Guid.Parse("019a91b2-4c6e-7d13-9a58-3e7f2b6c1d01");
    private static readonly DateTimeOffset ReceivedAt = new(2026, 11, 17, 10, 15, 30, TimeSpan.Zero);

    [Fact]
    public void ToMessage_MotionReading_ReturnsMotionMessage()
    {
        // Arrange
        var reading = new MotionLiveReadingModel
        {
            Id = ReadingId,
            Location = MeterLocation.Garage,
            ReceivedAt = ReceivedAt,
            MotionDetected = true
        };

        // Act
        var message = reading.ToMessage();

        // Assert
        message.ShouldBe(new MotionLiveReading
        {
            Id = ReadingId,
            Location = MeterLocation.Garage,
            ReceivedAt = ReceivedAt,
            MotionDetected = true
        });
        message.Type.ShouldBe(MeterType.Motion);
    }

    [Fact]
    public void ToMessage_EnergyReading_ReturnsEnergyMessage()
    {
        // Arrange
        var reading = new EnergyLiveReadingModel
        {
            Id = ReadingId,
            Location = MeterLocation.Office,
            ReceivedAt = ReceivedAt,
            Energy = 845.62
        };

        // Act
        var message = reading.ToMessage();

        // Assert
        message.ShouldBe(new EnergyLiveReading
        {
            Id = ReadingId,
            Location = MeterLocation.Office,
            ReceivedAt = ReceivedAt,
            Energy = 845.62
        });
        message.Type.ShouldBe(MeterType.Energy);
    }

    [Fact]
    public void ToMessage_AirQualityReading_ReturnsAirQualityMessage()
    {
        // Arrange
        var reading = new AirQualityLiveReadingModel
        {
            Id = ReadingId,
            Location = MeterLocation.LivingRoom,
            ReceivedAt = ReceivedAt,
            Co2 = 803,
            Pm25 = 25,
            Humidity = 36
        };

        // Act
        var message = reading.ToMessage();

        // Assert
        message.ShouldBe(new AirQualityLiveReading
        {
            Id = ReadingId,
            Location = MeterLocation.LivingRoom,
            ReceivedAt = ReceivedAt,
            Co2 = 803,
            Pm25 = 25,
            Humidity = 36
        });
        message.Type.ShouldBe(MeterType.AirQuality);
    }

    [Fact]
    public void ToMessage_UnknownReadingModel_ThrowsUnreachableException()
    {
        // Arrange
        var reading = new UnknownLiveReadingModel
        {
            Id = ReadingId,
            Location = MeterLocation.Kitchen,
            ReceivedAt = ReceivedAt
        };

        // Act & Assert
        Should.Throw<UnreachableException>(() => reading.ToMessage());
    }

    private sealed record UnknownLiveReadingModel : LiveReadingModel
    {
        public override MeterType Type => MeterType.Energy;
    }
}
