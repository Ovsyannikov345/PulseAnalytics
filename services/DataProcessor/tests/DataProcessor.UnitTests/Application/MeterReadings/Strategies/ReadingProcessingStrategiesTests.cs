using DataProcessor.Application.Interfaces.Database;
using DataProcessor.Application.MeterReadings.Models;
using DataProcessor.Application.MeterReadings.Strategies;
using DataProcessor.Application.Telemetry.Metrics;
using DataProcessor.Application.Telemetry.Metrics.Enums;
using DataProcessor.Domain.Constants.Enums;
using DataProcessor.Domain.Entities.MeterReadings;
using DataProcessor.Domain.Entities.MeterReadings.Payloads;
using DataProcessor.UnitTests.TestDoubles;
using FakeItEasy;
using FluentValidation;
using FluentValidation.Results;
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
    private readonly IValidator<MotionReadingModel> _motionValidator = A.Fake<IValidator<MotionReadingModel>>();
    private readonly IValidator<EnergyReadingModel> _energyValidator = A.Fake<IValidator<EnergyReadingModel>>();
    private readonly IValidator<AirQualityReadingModel> _airQualityValidator = A.Fake<IValidator<AirQualityReadingModel>>();
    private readonly IMetricsService _metrics = A.Fake<IMetricsService>();
    private readonly FakeLogCollector _logCollector = new();

    public ReadingProcessingStrategiesTests()
    {
        StoredReadingsAre(CreateStoredReading(Guid.CreateVersion7()));
        ValidationReturns(new ValidationResult());
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

    [Theory]
    [InlineData(StrategyKind.Motion)]
    [InlineData(StrategyKind.Energy)]
    [InlineData(StrategyKind.AirQuality)]
    public async Task ProcessAsync_InvalidReading_FailsWithValidationErrorsWithoutTouchingDatabase(StrategyKind kind)
    {
        // Arrange
        StoredReadingsAre(CreateStoredReading(DeduplicationKey));
        ValidationReturns(new ValidationResult(
        [
            new ValidationFailure("ReceivedAt", "'Received At' must not be in the future."),
            new ValidationFailure("Location", "'Location' has a range of values which does not include '99'.")
        ]));

        // Act
        var result = await CreateSut(kind).ProcessAsync(CreateModel(kind), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.ErrorMessage.ShouldBe(
            "'Received At' must not be in the future.; 'Location' has a range of values which does not include '99'.");
        A.CallTo(_dbContext).MustNotHaveHappened();
    }

    [Fact]
    public async Task ProcessAsync_InvalidReading_LogsThatItIsRejected()
    {
        // Arrange
        ValidationReturns(new ValidationResult(
        [
            new ValidationFailure("Energy", "'Energy' must be a finite number.")
        ]));

        // Act
        await CreateSut(StrategyKind.Energy).ProcessAsync(CreateModel(StrategyKind.Energy), TestContext.Current.CancellationToken);

        // Assert
        var entry = _logCollector.GetSnapshot().ShouldHaveSingleItem();
        entry.Level.ShouldBe(LogLevel.Warning);
        entry.Message.ShouldBe(
            $"Rejected invalid Energy reading {DeduplicationKey} for location Office. 'Energy' must be a finite number.");
    }

    [Theory]
    [InlineData(StrategyKind.Motion, MeterType.Motion, MeterLocation.Garage)]
    [InlineData(StrategyKind.Energy, MeterType.Energy, MeterLocation.Office)]
    [InlineData(StrategyKind.AirQuality, MeterType.AirQuality, MeterLocation.Kitchen)]
    public async Task ProcessAsync_NewReading_RecordsStoredReadingWithItsTypeAndLocation(
        StrategyKind kind,
        MeterType expectedType,
        MeterLocation expectedLocation)
    {
        // Act
        await CreateSut(kind).ProcessAsync(CreateModel(kind), TestContext.Current.CancellationToken);

        // Assert
        A.CallTo(_metrics).MustHaveHappenedOnceExactly();
        A.CallTo(() => _metrics.RecordReading(expectedType, expectedLocation, ReadingOutcome.Stored))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task ProcessAsync_SavingChangesThrows_RecordsNothing()
    {
        // Arrange
        A.CallTo(() => _dbContext.SaveChangesAsync(A<CancellationToken>._))
            .ThrowsAsync(new InvalidOperationException("Database is unavailable"));

        // Act
        var act = () => CreateSut(StrategyKind.Energy).ProcessAsync(CreateModel(StrategyKind.Energy), TestContext.Current.CancellationToken);

        // Assert
        await act.ShouldThrowAsync<InvalidOperationException>();
        A.CallTo(_metrics).MustNotHaveHappened();
    }

    [Fact]
    public async Task ProcessAsync_ReadingWithStoredDeduplicationKey_RecordsDuplicateReading()
    {
        // Arrange
        StoredReadingsAre(CreateStoredReading(DeduplicationKey));

        // Act
        await CreateSut(StrategyKind.Energy).ProcessAsync(CreateModel(StrategyKind.Energy), TestContext.Current.CancellationToken);

        // Assert
        A.CallTo(_metrics).MustHaveHappenedOnceExactly();
        A.CallTo(() => _metrics.RecordReading(MeterType.Energy, MeterLocation.Office, ReadingOutcome.Duplicate))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task ProcessAsync_InvalidReading_RecordsInvalidReading()
    {
        // Arrange
        ValidationReturns(new ValidationResult(
        [
            new ValidationFailure("Energy", "'Energy' must be a finite number.")
        ]));

        // Act
        await CreateSut(StrategyKind.Energy).ProcessAsync(CreateModel(StrategyKind.Energy), TestContext.Current.CancellationToken);

        // Assert
        A.CallTo(_metrics).MustHaveHappenedOnceExactly();
        A.CallTo(() => _metrics.RecordReading(MeterType.Energy, MeterLocation.Office, ReadingOutcome.Invalid))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task ProcessAsync_ModelOfOtherReadingType_RecordsNothing()
    {
        // Act
        await CreateSut(StrategyKind.Motion).ProcessAsync(CreateModel(StrategyKind.Energy), TestContext.Current.CancellationToken);

        // Assert
        A.CallTo(_metrics).MustNotHaveHappened();
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
                _motionValidator,
                _metrics,
                new FakeLogger<MotionReadingProcessingStrategy>(_logCollector)),
            StrategyKind.Energy => new EnergyReadingProcessingStrategy(
                _dbContext,
                _energyValidator,
                _metrics,
                new FakeLogger<EnergyReadingProcessingStrategy>(_logCollector)),
            _ => new AirQualityReadingProcessingStrategy(
                _dbContext,
                _airQualityValidator,
                _metrics,
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

    private void ValidationReturns(ValidationResult validationResult)
    {
        A.CallTo(() => _motionValidator.ValidateAsync(A<MotionReadingModel>._, A<CancellationToken>._))
            .Returns(validationResult);
        A.CallTo(() => _energyValidator.ValidateAsync(A<EnergyReadingModel>._, A<CancellationToken>._))
            .Returns(validationResult);
        A.CallTo(() => _airQualityValidator.ValidateAsync(A<AirQualityReadingModel>._, A<CancellationToken>._))
            .Returns(validationResult);
    }
}
