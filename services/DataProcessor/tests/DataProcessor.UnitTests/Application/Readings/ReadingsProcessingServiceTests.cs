using Ausiannikau.Utilities.Results;
using DataProcessor.Application.Interfaces.Database;
using DataProcessor.Application.Readings;
using DataProcessor.Application.Readings.Models;
using DataProcessor.Application.Readings.Strategies;
using DataProcessor.Domain.Constants.Enums;
using DataProcessor.Domain.Entities;
using DataProcessor.Domain.Entities.Payloads;
using DataProcessor.UnitTests.TestDoubles;
using FakeItEasy;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using Shouldly;
using Xunit;

namespace DataProcessor.UnitTests.Application.Readings;

public sealed class ReadingsProcessingServiceTests
{
    private static readonly DateTimeOffset ReceivedAt = new(2026, 11, 1, 12, 0, 0, TimeSpan.Zero);

    private readonly IReadingProcessingStrategy _motionStrategy = CreateStrategy<MotionReadingModel>();
    private readonly IReadingProcessingStrategy _energyStrategy = CreateStrategy<EnergyReadingModel>();
    private readonly IApplicationDbContext _dbContext = A.Fake<IApplicationDbContext>();
    private readonly FakeLogger<ReadingsProcessingService> _logger = new();
    private readonly ReadingsProcessingService _sut;

    public ReadingsProcessingServiceTests()
    {
        StoredReadingsAre(CreateMeterReading(Guid.CreateVersion7()));

        _sut = new ReadingsProcessingService([_motionStrategy, _energyStrategy], _dbContext, _logger);
    }

    [Fact]
    public async Task ProcessAsync_NewReading_CreatesReadingWithStrategyOfItsModelType()
    {
        // Arrange
        var model = CreateEnergyModel();

        // Act
        await _sut.ProcessAsync(model, TestContext.Current.CancellationToken);

        // Assert
        A.CallTo(() => _energyStrategy.CreateReading(model)).MustHaveHappenedOnceExactly();
        A.CallTo(() => _motionStrategy.CreateReading(A<MeterReadingModel>._)).MustNotHaveHappened();
    }

    [Fact]
    public async Task ProcessAsync_NewReading_AddsCreatedReadingAndThenSavesChanges()
    {
        // Arrange
        var model = CreateEnergyModel();
        var reading = CreateMeterReading(model.DeduplicationKey);
        A.CallTo(() => _energyStrategy.CreateReading(model)).Returns(Result<MeterReading>.Succeed(reading));

        // Act
        var result = await _sut.ProcessAsync(model, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        A.CallTo(() => _dbContext.Add(reading))
            .MustHaveHappenedOnceExactly()
            .Then(A.CallTo(() => _dbContext.SaveChangesAsync(A<CancellationToken>._)).MustHaveHappenedOnceExactly());
    }

    [Fact]
    public async Task ProcessAsync_ReadingWithStoredDeduplicationKey_SucceedsWithoutStoringIt()
    {
        // Arrange
        var model = CreateEnergyModel();
        StoredReadingsAre(CreateMeterReading(model.DeduplicationKey));

        // Act
        var result = await _sut.ProcessAsync(model, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        A.CallTo(() => _energyStrategy.CreateReading(A<MeterReadingModel>._)).MustNotHaveHappened();
        A.CallTo(() => _dbContext.Add(A<MeterReading>._)).MustNotHaveHappened();
        A.CallTo(() => _dbContext.SaveChangesAsync(A<CancellationToken>._)).MustNotHaveHappened();
    }

    [Fact]
    public async Task ProcessAsync_ReadingWithStoredDeduplicationKey_LogsThatItIsSkipped()
    {
        // Arrange
        var model = CreateEnergyModel();
        StoredReadingsAre(CreateMeterReading(model.DeduplicationKey));

        // Act
        await _sut.ProcessAsync(model, TestContext.Current.CancellationToken);

        // Assert
        var entry = _logger.Collector.GetSnapshot().ShouldHaveSingleItem();
        entry.Level.ShouldBe(LogLevel.Information);
        entry.Message.ShouldBe($"Reading {model.DeduplicationKey} for location Office is already stored, skipped");
    }

    [Fact]
    public async Task ProcessAsync_NoStrategyForModelType_FailsWithoutStoringIt()
    {
        // Arrange
        var model = new AirQualityReadingModel
        {
            DeduplicationKey = Guid.CreateVersion7(),
            Location = "Kitchen",
            ReceivedAt = ReceivedAt,
            Co2 = 803,
            Pm25 = 25,
            Humidity = 36
        };

        // Act
        var result = await _sut.ProcessAsync(model, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.ErrorMessage.ShouldBe("No processing strategy for AirQualityReadingModel");
        A.CallTo(() => _dbContext.Add(A<MeterReading>._)).MustNotHaveHappened();
        A.CallTo(() => _dbContext.SaveChangesAsync(A<CancellationToken>._)).MustNotHaveHappened();
        _logger.Collector.GetSnapshot().ShouldContain(entry => entry.Level == LogLevel.Error);
    }

    [Fact]
    public async Task ProcessAsync_StrategyFails_ReturnsItsFailureWithoutStoringAnything()
    {
        // Arrange
        var model = CreateEnergyModel();
        A.CallTo(() => _energyStrategy.CreateReading(model)).Returns(Result<MeterReading>.Fail("Invalid reading"));

        // Act
        var result = await _sut.ProcessAsync(model, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.ErrorMessage.ShouldBe("Invalid reading");
        A.CallTo(() => _dbContext.Add(A<MeterReading>._)).MustNotHaveHappened();
        A.CallTo(() => _dbContext.SaveChangesAsync(A<CancellationToken>._)).MustNotHaveHappened();
    }

    private static IReadingProcessingStrategy CreateStrategy<TModel>()
        where TModel : MeterReadingModel
    {
        var strategy = A.Fake<IReadingProcessingStrategy>();

        A.CallTo(() => strategy.ModelType).Returns(typeof(TModel));
        A.CallTo(() => strategy.CreateReading(A<MeterReadingModel>._))
            .ReturnsLazily((MeterReadingModel model) => Result<MeterReading>.Succeed(CreateMeterReading(model.DeduplicationKey)));

        return strategy;
    }

    private static EnergyReadingModel CreateEnergyModel()
    {
        return new EnergyReadingModel
        {
            DeduplicationKey = Guid.CreateVersion7(),
            Location = "Office",
            ReceivedAt = ReceivedAt,
            Energy = 845.62
        };
    }

    private static MeterReading CreateMeterReading(Guid deduplicationKey)
    {
        return new MeterReading
        {
            DeduplicationKey = deduplicationKey,
            Type = MeterType.Energy,
            Location = "Office",
            ReceivedAt = ReceivedAt,
            Payload = new EnergyPayload { Energy = 845.62 }
        };
    }

    private void StoredReadingsAre(params MeterReading[] readings)
    {
        A.CallTo(() => _dbContext.Query<MeterReading>()).ReturnsLazily(() => readings.AsAsyncQueryable());
    }
}
