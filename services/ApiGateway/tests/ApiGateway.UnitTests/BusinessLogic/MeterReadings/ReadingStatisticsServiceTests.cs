using ApiGateway.BusinessLogic.MeterReadings;
using ApiGateway.BusinessLogic.MeterReadings.Models;
using ApiGateway.BusinessLogic.MeterReadings.Requests;
using ApiGateway.BusinessLogic.MeterReadings.Validators;
using ApiGateway.Common.Constants.Enums;
using ApiGateway.DataAccess.Database;
using ApiGateway.DataAccess.Entities;
using ApiGateway.UnitTests.TestDoubles;
using ApiGateway.UnitTests.Utilities;
using FakeItEasy;
using Shouldly;
using Xunit;

namespace ApiGateway.UnitTests.BusinessLogic.MeterReadings;

// The real validators are used: they are pure, and the service's contract includes rejecting what they reject.
// The queries run in memory (date_bin through its C# body), so these tests check the query logic;
// the SQL is checked by the translation tests and the integration tests.
public sealed class ReadingStatisticsServiceTests
{
    private static readonly DateTimeOffset Start = new(2026, 11, 4, 12, 0, 0, TimeSpan.Zero);

    private readonly IReadOnlyDbContext _dbContext = A.Fake<IReadOnlyDbContext>();
    private readonly List<MeterReading> _storedReadings = [];
    private readonly ReadingStatisticsService _sut;

    public ReadingStatisticsServiceTests()
    {
        A.CallTo(() => _dbContext.Query<MeterReading>())
            .ReturnsLazily(() => _storedReadings.AsAsyncQueryable());

        _sut = new ReadingStatisticsService(
            _dbContext,
            new GetEnergyStatisticsRequestValidator(),
            new GetAirQualityStatisticsRequestValidator(),
            new GetMotionStatisticsRequestValidator());
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task GetEnergyStatisticsAsync_ByLocationAndHour_ReturnsStatisticsPerGroupOrderedByLocationAndBucket()
    {
        // Arrange
        StoreEnergySample();

        // Act
        var result = await _sut.GetEnergyStatisticsAsync(
            EnergyRequest([StatisticsGrouping.Location, StatisticsGrouping.Time], TimeBucket.Hour),
            Ct);

        // Assert
        result.Value.ShouldBe(
        [
            new EnergyStatisticsModel { Location = MeterLocation.Kitchen, BucketStart = Start, Samples = 2, Energy = Metric(3, 2, 4) },
            new EnergyStatisticsModel { Location = MeterLocation.Kitchen, BucketStart = Start.AddHours(1), Samples = 1, Energy = Metric(6, 6, 6) },
            new EnergyStatisticsModel { Location = MeterLocation.Office, BucketStart = Start, Samples = 1, Energy = Metric(6, 6, 6) }
        ]);
    }

    [Fact]
    public async Task GetEnergyStatisticsAsync_ByHour_ReturnsStatisticsPerBucketWithoutLocation()
    {
        // Arrange
        StoreEnergySample();

        // Act
        var result = await _sut.GetEnergyStatisticsAsync(EnergyRequest([StatisticsGrouping.Time], TimeBucket.Hour), Ct);

        // Assert
        result.Value.ShouldBe(
        [
            new EnergyStatisticsModel { BucketStart = Start, Samples = 3, Energy = Metric(4, 2, 6) },
            new EnergyStatisticsModel { BucketStart = Start.AddHours(1), Samples = 1, Energy = Metric(6, 6, 6) }
        ]);
    }

    [Fact]
    public async Task GetEnergyStatisticsAsync_ByLocation_ReturnsStatisticsPerLocationWithoutBucket()
    {
        // Arrange
        StoreEnergySample();

        // Act
        var result = await _sut.GetEnergyStatisticsAsync(EnergyRequest([StatisticsGrouping.Location], bucket: null), Ct);

        // Assert
        result.Value.ShouldBe(
        [
            new EnergyStatisticsModel { Location = MeterLocation.Kitchen, Samples = 3, Energy = Metric(4, 2, 6) },
            new EnergyStatisticsModel { Location = MeterLocation.Office, Samples = 1, Energy = Metric(6, 6, 6) }
        ]);
    }

    [Fact]
    public async Task GetEnergyStatisticsAsync_NoGrouping_ReturnsOneRowForTheWholeRange()
    {
        // Arrange
        StoreEnergySample();

        // Act
        var result = await _sut.GetEnergyStatisticsAsync(EnergyRequest([], bucket: null), Ct);

        // Assert
        result.Value.ShouldHaveSingleItem().ShouldBe(
            new EnergyStatisticsModel { Samples = 4, Energy = Metric(4.5, 2, 6) });
    }

    [Fact]
    public async Task GetEnergyStatisticsAsync_NoReadings_ReturnsEmptyList()
    {
        // Act
        var result = await _sut.GetEnergyStatisticsAsync(EnergyRequest([], bucket: null), Ct);

        // Assert
        result.Value.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetEnergyStatisticsAsync_Filter_AggregatesMatchingEnergyReadingsOnly()
    {
        // Arrange
        var to = Start.AddHours(1);
        _storedReadings.AddRange(
        [
            StoredReadings.Energy(MeterLocation.Kitchen, Start.AddTicks(-10), energy: 100),
            StoredReadings.Energy(MeterLocation.Kitchen, Start, energy: 2),
            StoredReadings.Energy(MeterLocation.Kitchen, to.AddTicks(-10), energy: 4),
            StoredReadings.Energy(MeterLocation.Kitchen, to, energy: 100),
            StoredReadings.Energy(MeterLocation.Garage, Start, energy: 100),
            StoredReadings.Motion(MeterLocation.Kitchen, Start),
            StoredReadings.AirQuality(MeterLocation.Kitchen, Start)
        ]);

        var request = new GetEnergyStatisticsRequest { Locations = [MeterLocation.Kitchen], From = Start, To = to };

        // Act
        var result = await _sut.GetEnergyStatisticsAsync(request, Ct);

        // Assert
        result.Value.ShouldHaveSingleItem().ShouldBe(
            new EnergyStatisticsModel { Samples = 2, Energy = Metric(3, 2, 4) });
    }

    [Fact]
    public async Task GetEnergyStatisticsAsync_DayBuckets_StartAtUtcMidnight()
    {
        // Arrange
        // 23:30 at UTC+2 is 21:30 UTC on the same day; 00:30 at UTC+2 is still the previous day in UTC.
        var plusTwo = TimeSpan.FromHours(2);
        _storedReadings.AddRange(
        [
            StoredReadings.Energy(MeterLocation.Kitchen, new DateTimeOffset(2026, 11, 4, 23, 30, 0, plusTwo)),
            StoredReadings.Energy(MeterLocation.Kitchen, new DateTimeOffset(2026, 11, 5, 0, 30, 0, plusTwo)),
            StoredReadings.Energy(MeterLocation.Kitchen, new DateTimeOffset(2026, 11, 5, 2, 30, 0, plusTwo))
        ]);

        var request = new GetEnergyStatisticsRequest
        {
            From = new DateTimeOffset(2026, 11, 4, 0, 0, 0, TimeSpan.Zero),
            To = new DateTimeOffset(2026, 11, 6, 0, 0, 0, TimeSpan.Zero),
            GroupBy = [StatisticsGrouping.Time],
            Bucket = TimeBucket.Day
        };

        // Act
        var result = await _sut.GetEnergyStatisticsAsync(request, Ct);

        // Assert
        result.Value.Select(s => (s.BucketStart, s.Samples)).ShouldBe(
        [
            (new DateTimeOffset(2026, 11, 4, 0, 0, 0, TimeSpan.Zero), 2),
            (new DateTimeOffset(2026, 11, 5, 0, 0, 0, TimeSpan.Zero), 1)
        ]);
    }

    [Fact]
    public async Task GetEnergyStatisticsAsync_InvalidRequest_FailsWithoutQuerying()
    {
        // Act
        var result = await _sut.GetEnergyStatisticsAsync(EnergyRequest([StatisticsGrouping.Time], bucket: null), Ct);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.ErrorMessage.ShouldBe("'Bucket' is required when grouping by time.");
        A.CallTo(() => _dbContext.Query<MeterReading>()).MustNotHaveHappened();
    }

    [Fact]
    public async Task GetAirQualityStatisticsAsync_ByLocation_ReturnsStatisticsOfEachMetric()
    {
        // Arrange
        _storedReadings.AddRange(
        [
            StoredReadings.AirQuality(MeterLocation.Kitchen, Start, co2: 600, pm25: 10, humidity: 40),
            StoredReadings.AirQuality(MeterLocation.Kitchen, Start.AddMinutes(1), co2: 800, pm25: 30, humidity: 45),
            StoredReadings.AirQuality(MeterLocation.Bedroom, Start, co2: 500, pm25: 5, humidity: 50),
            StoredReadings.Energy(MeterLocation.Kitchen, Start)
        ]);

        var request = new GetAirQualityStatisticsRequest
        {
            From = Start,
            To = Start.AddHours(1),
            GroupBy = [StatisticsGrouping.Location]
        };

        // Act
        var result = await _sut.GetAirQualityStatisticsAsync(request, Ct);

        // Assert
        result.Value.ShouldBe(
        [
            new AirQualityStatisticsModel
            {
                Location = MeterLocation.Kitchen,
                Samples = 2,
                Co2 = Metric(700, 600, 800),
                Pm25 = Metric(20, 10, 30),
                Humidity = Metric(42.5, 40, 45)
            },
            new AirQualityStatisticsModel
            {
                Location = MeterLocation.Bedroom,
                Samples = 1,
                Co2 = Metric(500, 500, 500),
                Pm25 = Metric(5, 5, 5),
                Humidity = Metric(50, 50, 50)
            }
        ]);
    }

    [Fact]
    public async Task GetAirQualityStatisticsAsync_InvalidRequest_FailsWithoutQuerying()
    {
        // Arrange
        var request = new GetAirQualityStatisticsRequest { From = Start, To = Start.AddDays(32) };

        // Act
        var result = await _sut.GetAirQualityStatisticsAsync(request, Ct);

        // Assert
        result.IsFailure.ShouldBeTrue();
        A.CallTo(() => _dbContext.Query<MeterReading>()).MustNotHaveHappened();
    }

    [Fact]
    public async Task GetMotionStatisticsAsync_ByFifteenMinutes_CountsMotionSamplesPerBucket()
    {
        // Arrange
        _storedReadings.AddRange(
        [
            StoredReadings.Motion(MeterLocation.Garage, Start, motionDetected: true),
            StoredReadings.Motion(MeterLocation.Garage, Start.AddMinutes(5), motionDetected: false),
            StoredReadings.Motion(MeterLocation.Corridor, Start.AddMinutes(10), motionDetected: true),
            StoredReadings.Motion(MeterLocation.Corridor, Start.AddMinutes(14), motionDetected: false),
            StoredReadings.Motion(MeterLocation.Garage, Start.AddMinutes(15), motionDetected: false),
            StoredReadings.Energy(MeterLocation.Garage, Start)
        ]);

        var request = new GetMotionStatisticsRequest
        {
            From = Start,
            To = Start.AddHours(1),
            GroupBy = [StatisticsGrouping.Time],
            Bucket = TimeBucket.FifteenMinutes
        };

        // Act
        var result = await _sut.GetMotionStatisticsAsync(request, Ct);

        // Assert
        result.Value.ShouldBe(
        [
            new MotionStatisticsModel { BucketStart = Start, Samples = 4, MotionSamples = 2 },
            new MotionStatisticsModel { BucketStart = Start.AddMinutes(15), Samples = 1, MotionSamples = 0 }
        ]);
        result.Value.Select(s => s.ActivityRatio).ShouldBe([0.5, 0]);
    }

    [Fact]
    public async Task GetMotionStatisticsAsync_InvalidRequest_FailsWithoutQuerying()
    {
        // Arrange
        var request = new GetMotionStatisticsRequest { From = Start, To = Start, Locations = [(MeterLocation)99] };

        // Act
        var result = await _sut.GetMotionStatisticsAsync(request, Ct);

        // Assert
        result.IsFailure.ShouldBeTrue();
        A.CallTo(() => _dbContext.Query<MeterReading>()).MustNotHaveHappened();
    }

    private static GetEnergyStatisticsRequest EnergyRequest(StatisticsGrouping[] groupBy, TimeBucket? bucket)
    {
        return new GetEnergyStatisticsRequest
        {
            From = Start,
            To = Start.AddHours(2),
            GroupBy = groupBy,
            Bucket = bucket
        };
    }

    private static MetricStatisticsModel Metric(double average, double minimum, double maximum)
    {
        return new MetricStatisticsModel { Average = average, Minimum = minimum, Maximum = maximum };
    }

    // Energy: Kitchen 2 and 4 at 12:xx, 6 at 13:10; Office 6 at 12:15. A Kitchen motion reading is not energy.
    private void StoreEnergySample()
    {
        _storedReadings.AddRange(
        [
            StoredReadings.Energy(MeterLocation.Kitchen, Start, energy: 2),
            StoredReadings.Energy(MeterLocation.Kitchen, Start.AddMinutes(30), energy: 4),
            StoredReadings.Energy(MeterLocation.Kitchen, Start.AddMinutes(70), energy: 6),
            StoredReadings.Energy(MeterLocation.Office, Start.AddMinutes(15), energy: 6),
            StoredReadings.Motion(MeterLocation.Kitchen, Start)
        ]);
    }
}
