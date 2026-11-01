using DataProcessor.Application.Interfaces.Database;
using DataProcessor.Application.MeterReadings.Models;
using DataProcessor.Application.MeterReadings.Strategies;
using DataProcessor.Domain.Constants.Enums;
using DataProcessor.Domain.Entities.MeterReadings;
using DataProcessor.Domain.Entities.MeterReadings.Payloads;
using DataProcessor.UnitTests.TestDoubles;
using FakeItEasy;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using Shouldly;
using Xunit;

namespace DataProcessor.UnitTests.Application.MeterReadings.Strategies;

// Covers the shared logic of ReadingProcessingStrategy through each concrete strategy.
public sealed class ReadingProcessingStrategiesTests
{
    private static readonly Guid DeduplicationKey = Guid.Parse("0199f5c4-3a1e-7b52-9d0e-5c8a7e6b4f21");

    private static readonly DateTimeOffset ReceivedAt = new(2026, 11, 1, 12, 0, 0, TimeSpan.Zero);

    private readonly IApplicationDbContext _dbContext = A.Fake<IApplicationDbContext>();
    private readonly FakeLogCollector _logCollector = new();

    public ReadingProcessingStrategiesTests()
    {
        StoredReadingsAre(CreateStoredReading(Guid.CreateVersion7()));
    }

    public enum StrategyKind
    {
        Motion,
        Energy,
        AirQuality
    }

    [Theory]
    [InlineData(StrategyKind.Motion, StrategyKind.Motion, true)]
    [InlineData(StrategyKind.Motion, StrategyKind.Energy, false)]
    [InlineData(StrategyKind.Motion, StrategyKind.AirQuality, false)]
    [InlineData(StrategyKind.Energy, StrategyKind.Energy, true)]
    [InlineData(StrategyKind.Energy, StrategyKind.Motion, false)]
    [InlineData(StrategyKind.Energy, StrategyKind.AirQuality, false)]
    [InlineData(StrategyKind.AirQuality, StrategyKind.AirQuality, true)]
    [InlineData(StrategyKind.AirQuality, StrategyKind.Motion, false)]
    [InlineData(StrategyKind.AirQuality, StrategyKind.Energy, false)]
    public void CanProcess_ModelOfAnyReadingType_IsTrueOnlyForItsOwnReadingType(
        StrategyKind strategyKind,
        StrategyKind modelKind,
        bool expected)
    {
        // Act
        var canProcess = CreateSut(strategyKind).CanProcess(CreateModel(modelKind));

        // Assert
        canProcess.ShouldBe(expected);
    }

    [Fact]
    public async Task ProcessAsync_NewMotionReading_AddsMotionMeterReading()
    {
        // Act
        var result = await CreateSut(StrategyKind.Motion)
            .ProcessAsync(CreateModel(StrategyKind.Motion), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        var reading = ShouldHaveAddedReading(MeterType.Motion, MeterLocation.Garage);
        reading.Payload.ShouldBe(new MotionPayload { MotionDetected = true });
    }

    [Fact]
    public async Task ProcessAsync_NewEnergyReading_AddsEnergyMeterReading()
    {
        // Act
        var result = await CreateSut(StrategyKind.Energy)
            .ProcessAsync(CreateModel(StrategyKind.Energy), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        var reading = ShouldHaveAddedReading(MeterType.Energy, MeterLocation.Office);
        reading.Payload.ShouldBe(new EnergyPayload { Energy = 845.62 });
    }

    [Fact]
    public async Task ProcessAsync_NewAirQualityReading_AddsAirQualityMeterReading()
    {
        // Act
        var result = await CreateSut(StrategyKind.AirQuality)
            .ProcessAsync(CreateModel(StrategyKind.AirQuality), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        var reading = ShouldHaveAddedReading(MeterType.AirQuality, MeterLocation.Kitchen);
        reading.Payload.ShouldBe(new AirQualityPayload { Co2 = 803, Pm25 = 25, Humidity = 36 });
    }

    [Theory]
    [InlineData(StrategyKind.Motion)]
    [InlineData(StrategyKind.Energy)]
    [InlineData(StrategyKind.AirQuality)]
    public async Task ProcessAsync_NewReading_AddsReadingAndThenSavesChanges(StrategyKind kind)
    {
        // Act
        await CreateSut(kind).ProcessAsync(CreateModel(kind), TestContext.Current.CancellationToken);

        // Assert
        A.CallTo(() => _dbContext.Add(A<MeterReading>._))
            .MustHaveHappenedOnceExactly()
            .Then(A.CallTo(() => _dbContext.SaveChangesAsync(A<CancellationToken>._)).MustHaveHappenedOnceExactly());
    }

    [Theory]
    [InlineData(StrategyKind.Motion)]
    [InlineData(StrategyKind.Energy)]
    [InlineData(StrategyKind.AirQuality)]
    public async Task ProcessAsync_ReadingWithStoredDeduplicationKey_SucceedsWithoutStoringIt(StrategyKind kind)
    {
        // Arrange
        StoredReadingsAre(CreateStoredReading(DeduplicationKey));

        // Act
        var result = await CreateSut(kind).ProcessAsync(CreateModel(kind), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        A.CallTo(() => _dbContext.Add(A<MeterReading>._)).MustNotHaveHappened();
        A.CallTo(() => _dbContext.SaveChangesAsync(A<CancellationToken>._)).MustNotHaveHappened();
    }

    [Fact]
    public async Task ProcessAsync_ReadingWithStoredDeduplicationKey_LogsThatItIsSkipped()
    {
        // Arrange
        StoredReadingsAre(CreateStoredReading(DeduplicationKey));

        // Act
        await CreateSut(StrategyKind.Energy).ProcessAsync(CreateModel(StrategyKind.Energy), TestContext.Current.CancellationToken);

        // Assert
        var entry = _logCollector.GetSnapshot().ShouldHaveSingleItem();
        entry.Level.ShouldBe(LogLevel.Information);
        entry.Message.ShouldBe($"Reading {DeduplicationKey} for location Office is already stored, skipped");
    }

    [Theory]
    [InlineData(StrategyKind.Motion)]
    [InlineData(StrategyKind.AirQuality)]
    public async Task ProcessAsync_ModelOfOtherReadingType_FailsWithoutTouchingDatabase(StrategyKind kind)
    {
        // Arrange
        var sut = CreateSut(kind);

        // Act
        var result = await sut.ProcessAsync(CreateModel(StrategyKind.Energy), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.ErrorMessage.ShouldBe($"{sut.GetType().Name} cannot process a EnergyReadingModel");
        A.CallTo(_dbContext).MustNotHaveHappened();
    }

    private static MeterReadingModel CreateModel(StrategyKind kind)
    {
        return kind switch
        {
            StrategyKind.Motion => new MotionReadingModel
            {
                DeduplicationKey = DeduplicationKey,
                Location = MeterLocation.Garage,
                ReceivedAt = ReceivedAt,
                MotionDetected = true
            },
            StrategyKind.Energy => new EnergyReadingModel
            {
                DeduplicationKey = DeduplicationKey,
                Location = MeterLocation.Office,
                ReceivedAt = ReceivedAt,
                Energy = 845.62
            },
            _ => new AirQualityReadingModel
            {
                DeduplicationKey = DeduplicationKey,
                Location = MeterLocation.Kitchen,
                ReceivedAt = ReceivedAt,
                Co2 = 803,
                Pm25 = 25,
                Humidity = 36
            }
        };
    }

    private static MeterReading CreateStoredReading(Guid deduplicationKey)
    {
        return new MeterReading
        {
            DeduplicationKey = deduplicationKey,
            Type = MeterType.Energy,
            Location = MeterLocation.Office,
            ReceivedAt = ReceivedAt,
            Payload = new EnergyPayload { Energy = 845.62 }
        };
    }

    private IReadingProcessingStrategy CreateSut(StrategyKind kind)
    {
        return kind switch
        {
            StrategyKind.Motion => new MotionReadingProcessingStrategy(
                _dbContext,
                new FakeLogger<MotionReadingProcessingStrategy>(_logCollector)),
            StrategyKind.Energy => new EnergyReadingProcessingStrategy(
                _dbContext,
                new FakeLogger<EnergyReadingProcessingStrategy>(_logCollector)),
            _ => new AirQualityReadingProcessingStrategy(
                _dbContext,
                new FakeLogger<AirQualityReadingProcessingStrategy>(_logCollector))
        };
    }

    private MeterReading ShouldHaveAddedReading(MeterType expectedType, MeterLocation expectedLocation)
    {
        var reading = Fake.GetCalls(_dbContext)
            .Where(call => call.Method.Name == nameof(IApplicationDbContext.Add))
            .ShouldHaveSingleItem()
            .GetArgument<MeterReading>(0)
            .ShouldNotBeNull();

        reading.DeduplicationKey.ShouldBe(DeduplicationKey);
        reading.Type.ShouldBe(expectedType);
        reading.Location.ShouldBe(expectedLocation);
        reading.ReceivedAt.ShouldBe(ReceivedAt);

        return reading;
    }

    private void StoredReadingsAre(params MeterReading[] readings)
    {
        A.CallTo(() => _dbContext.Query<MeterReading>()).ReturnsLazily(() => readings.AsAsyncQueryable());
    }
}
