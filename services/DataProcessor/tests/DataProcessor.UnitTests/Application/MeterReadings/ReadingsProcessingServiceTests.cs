using Ausiannikau.Utilities.Results;
using DataProcessor.Application.MeterReadings;
using DataProcessor.Application.MeterReadings.Models;
using DataProcessor.Application.MeterReadings.Strategies;
using DataProcessor.Domain.Constants.Enums;
using FakeItEasy;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using Shouldly;
using Xunit;

namespace DataProcessor.UnitTests.Application.MeterReadings;

public sealed class ReadingsProcessingServiceTests
{
    private static readonly DateTimeOffset ReceivedAt = new(2026, 11, 1, 12, 0, 0, TimeSpan.Zero);

    private readonly IReadingProcessingStrategy _motionStrategy = CreateStrategy<MotionReadingModel>();
    private readonly IReadingProcessingStrategy _energyStrategy = CreateStrategy<EnergyReadingModel>();
    private readonly FakeLogger<ReadingsProcessingService> _logger = new();
    private readonly ReadingsProcessingService _sut;

    public ReadingsProcessingServiceTests()
    {
        _sut = new ReadingsProcessingService([_motionStrategy, _energyStrategy], _logger);
    }

    [Fact]
    public async Task ProcessAsync_StrategyCanProcessReading_ProcessesItWithThatStrategyOnly()
    {
        // Arrange
        var model = CreateEnergyModel();

        // Act
        await _sut.ProcessAsync(model, TestContext.Current.CancellationToken);

        // Assert
        A.CallTo(() => _energyStrategy.ProcessAsync(model, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
        A.CallTo(() => _motionStrategy.ProcessAsync(A<MeterReadingModel>._, A<CancellationToken>._)).MustNotHaveHappened();
    }

    [Fact]
    public async Task ProcessAsync_StrategySucceeds_ReturnsSuccess()
    {
        // Act
        var result = await _sut.ProcessAsync(CreateEnergyModel(), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        _logger.Collector.GetSnapshot().ShouldBeEmpty();
    }

    [Fact]
    public async Task ProcessAsync_StrategyFails_ReturnsItsFailure()
    {
        // Arrange
        var model = CreateEnergyModel();
        A.CallTo(() => _energyStrategy.ProcessAsync(model, A<CancellationToken>._)).Returns(Result.Fail("Invalid reading"));

        // Act
        var result = await _sut.ProcessAsync(model, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.ErrorMessage.ShouldBe("Invalid reading");
    }

    [Fact]
    public async Task ProcessAsync_NoStrategyCanProcessReading_FailsAndLogsError()
    {
        // Arrange
        var model = new AirQualityReadingModel
        {
            DeduplicationKey = Guid.CreateVersion7(),
            Location = MeterLocation.Kitchen,
            ReceivedAt = ReceivedAt,
            Co2 = 803,
            Pm25 = 25,
            Humidity = 36
        };

        // Act
        var result = await _sut.ProcessAsync(model, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.ErrorMessage.ShouldBe("No processing strategy defined for AirQualityReadingModel");
        A.CallTo(() => _motionStrategy.ProcessAsync(A<MeterReadingModel>._, A<CancellationToken>._)).MustNotHaveHappened();
        A.CallTo(() => _energyStrategy.ProcessAsync(A<MeterReadingModel>._, A<CancellationToken>._)).MustNotHaveHappened();
        var entry = _logger.Collector.GetSnapshot().ShouldHaveSingleItem();
        entry.Level.ShouldBe(LogLevel.Error);
        entry.Message.ShouldBe("No processing strategy is registered for AirQualityReadingModel");
    }

    private static IReadingProcessingStrategy CreateStrategy<TModel>()
        where TModel : MeterReadingModel
    {
        var strategy = A.Fake<IReadingProcessingStrategy>();

        A.CallTo(() => strategy.CanProcess(A<MeterReadingModel>._)).ReturnsLazily((MeterReadingModel model) => model is TModel);
        A.CallTo(() => strategy.ProcessAsync(A<MeterReadingModel>._, A<CancellationToken>._)).Returns(Result.Succeed());

        return strategy;
    }

    private static EnergyReadingModel CreateEnergyModel()
    {
        return new EnergyReadingModel
        {
            DeduplicationKey = Guid.CreateVersion7(),
            Location = MeterLocation.Office,
            ReceivedAt = ReceivedAt,
            Energy = 845.62
        };
    }
}
