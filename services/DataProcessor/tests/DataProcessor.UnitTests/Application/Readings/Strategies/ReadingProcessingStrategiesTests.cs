using Ausiannikau.Utilities.Results;
using DataProcessor.Application.Readings.Models;
using DataProcessor.Application.Readings.Strategies;
using DataProcessor.Domain.Constants.Enums;
using DataProcessor.Domain.Entities;
using DataProcessor.Domain.Entities.Payloads;
using Shouldly;
using Xunit;

namespace DataProcessor.UnitTests.Application.Readings.Strategies;

// Covers the shared logic of ReadingProcessingStrategy through each concrete strategy.
public sealed class ReadingProcessingStrategiesTests
{
    private static readonly Guid DeduplicationKey = Guid.Parse("0199f5c4-3a1e-7b52-9d0e-5c8a7e6b4f21");

    private static readonly DateTimeOffset ReceivedAt = new(2026, 11, 1, 12, 0, 0, TimeSpan.Zero);

    public enum StrategyKind
    {
        Motion,
        Energy,
        AirQuality
    }

    [Theory]
    [InlineData(StrategyKind.Motion, typeof(MotionReadingModel))]
    [InlineData(StrategyKind.Energy, typeof(EnergyReadingModel))]
    [InlineData(StrategyKind.AirQuality, typeof(AirQualityReadingModel))]
    public void ModelType_AnyStrategy_IsModelTypeOfItsReadingType(StrategyKind kind, Type expectedModelType)
    {
        // Act
        var modelType = CreateSut(kind).ModelType;

        // Assert
        modelType.ShouldBe(expectedModelType);
    }

    [Fact]
    public void CreateReading_MotionReading_CreatesMotionMeterReading()
    {
        // Arrange
        var model = new MotionReadingModel
        {
            DeduplicationKey = DeduplicationKey,
            Location = "Garage",
            ReceivedAt = ReceivedAt,
            MotionDetected = true
        };

        // Act
        var result = new MotionReadingProcessingStrategy().CreateReading(model);

        // Assert
        var reading = ShouldBeCreated(result, MeterType.Motion, "Garage");
        reading.Payload.ShouldBe(new MotionPayload { MotionDetected = true });
    }

    [Fact]
    public void CreateReading_EnergyReading_CreatesEnergyMeterReading()
    {
        // Arrange
        var model = new EnergyReadingModel
        {
            DeduplicationKey = DeduplicationKey,
            Location = "Office",
            ReceivedAt = ReceivedAt,
            Energy = 845.62
        };

        // Act
        var result = new EnergyReadingProcessingStrategy().CreateReading(model);

        // Assert
        var reading = ShouldBeCreated(result, MeterType.Energy, "Office");
        reading.Payload.ShouldBe(new EnergyPayload { Energy = 845.62 });
    }

    [Fact]
    public void CreateReading_AirQualityReading_CreatesAirQualityMeterReading()
    {
        // Arrange
        var model = new AirQualityReadingModel
        {
            DeduplicationKey = DeduplicationKey,
            Location = "Kitchen",
            ReceivedAt = ReceivedAt,
            Co2 = 803,
            Pm25 = 25,
            Humidity = 36
        };

        // Act
        var result = new AirQualityReadingProcessingStrategy().CreateReading(model);

        // Assert
        var reading = ShouldBeCreated(result, MeterType.AirQuality, "Kitchen");
        reading.Payload.ShouldBe(new AirQualityPayload { Co2 = 803, Pm25 = 25, Humidity = 36 });
    }

    [Theory]
    [InlineData(StrategyKind.Motion)]
    [InlineData(StrategyKind.AirQuality)]
    public void CreateReading_ModelOfOtherReadingType_Fails(StrategyKind kind)
    {
        // Arrange
        var model = new EnergyReadingModel
        {
            DeduplicationKey = DeduplicationKey,
            Location = "Office",
            ReceivedAt = ReceivedAt,
            Energy = 845.62
        };
        var sut = CreateSut(kind);

        // Act
        var result = sut.CreateReading(model);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.ErrorMessage.ShouldBe($"{sut.GetType().Name} cannot process a EnergyReadingModel");
    }

    private static IReadingProcessingStrategy CreateSut(StrategyKind kind)
    {
        return kind switch
        {
            StrategyKind.Motion => new MotionReadingProcessingStrategy(),
            StrategyKind.Energy => new EnergyReadingProcessingStrategy(),
            _ => new AirQualityReadingProcessingStrategy()
        };
    }

    private static MeterReading ShouldBeCreated(
        Result<MeterReading> result,
        MeterType expectedType,
        string expectedLocation)
    {
        result.TryGetSuccessValue(out var reading).ShouldBeTrue();
        reading.DeduplicationKey.ShouldBe(DeduplicationKey);
        reading.Type.ShouldBe(expectedType);
        reading.Location.ShouldBe(expectedLocation);
        reading.ReceivedAt.ShouldBe(ReceivedAt);

        return reading;
    }
}
