using ApiGateway.BusinessLogic.MeterReadings.Mapping;
using ApiGateway.BusinessLogic.MeterReadings.Models;
using ApiGateway.Common.Constants.Enums;
using ApiGateway.DataAccess.Entities;
using ApiGateway.UnitTests.Utilities;
using Shouldly;
using Xunit;

namespace ApiGateway.UnitTests.BusinessLogic.MeterReadings.Mapping;

public sealed class MeterReadingMappingsTests
{
    private static readonly DateTimeOffset ReceivedAt = new(2026, 11, 4, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ToModel_MotionReading_ReturnsMotionModel()
    {
        // Arrange
        var reading = StoredReadings.Motion(MeterLocation.Corridor, ReceivedAt, motionDetected: true);

        // Act
        var model = reading.ToModel();

        // Assert
        model.ShouldBe(new MotionReadingModel
        {
            Id = reading.Id,
            Location = MeterLocation.Corridor,
            ReceivedAt = ReceivedAt,
            MotionDetected = true
        });
    }

    [Fact]
    public void ToModel_EnergyReading_ReturnsEnergyModel()
    {
        // Arrange
        var reading = StoredReadings.Energy(MeterLocation.Garage, ReceivedAt, energy: 3.25);

        // Act
        var model = reading.ToModel();

        // Assert
        model.ShouldBe(new EnergyReadingModel
        {
            Id = reading.Id,
            Location = MeterLocation.Garage,
            ReceivedAt = ReceivedAt,
            Energy = 3.25
        });
    }

    [Fact]
    public void ToModel_AirQualityReading_ReturnsAirQualityModel()
    {
        // Arrange
        var reading = StoredReadings.AirQuality(MeterLocation.Bedroom, ReceivedAt, co2: 812, pm25: 21, humidity: 40);

        // Act
        var model = reading.ToModel();

        // Assert
        model.ShouldBe(new AirQualityReadingModel
        {
            Id = reading.Id,
            Location = MeterLocation.Bedroom,
            ReceivedAt = ReceivedAt,
            Co2 = 812,
            Pm25 = 21,
            Humidity = 40
        });
    }

    [Fact]
    public void ToModel_ReadingWithoutItsTypeValue_Throws()
    {
        // Arrange
        var reading = new MeterReading
        {
            Id = Guid.CreateVersion7(),
            Type = MeterType.AirQuality,
            Location = MeterLocation.Kitchen,
            ReceivedAt = ReceivedAt,
            Co2 = 700,
            Pm25 = 10
        };

        // Act & Assert
        var exception = Should.Throw<InvalidOperationException>(() => reading.ToModel());
        exception.Message.ShouldContain(nameof(MeterReading.Humidity));
    }

    [Fact]
    public void ToModel_UnknownType_Throws()
    {
        // Arrange
        var reading = new MeterReading
        {
            Id = Guid.CreateVersion7(),
            Type = (MeterType)4,
            Location = MeterLocation.Kitchen,
            ReceivedAt = ReceivedAt
        };

        // Act & Assert
        Should.Throw<InvalidOperationException>(() => reading.ToModel());
    }
}
