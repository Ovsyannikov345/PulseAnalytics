using System.Text.Json;
using ApiGateway.Api.GraphQL.Errors;
using ApiGateway.BusinessLogic.MeterReadings;
using ApiGateway.BusinessLogic.MeterReadings.Models;
using ApiGateway.BusinessLogic.MeterReadings.Requests;
using ApiGateway.Common.Constants.Enums;
using ApiGateway.UnitTests.Utilities;
using Ausiannikau.Utilities.Results;
using FakeItEasy;
using Shouldly;
using Xunit;

namespace ApiGateway.UnitTests.Api.GraphQL.Queries;

// Runs GraphQL requests through the real schema and pipeline (argument binding, types, error filter)
// over a fake statistics service.
public sealed class ReadingStatisticsQueriesTests : IAsyncDisposable
{
    private static readonly DateTimeOffset From = new(2026, 11, 4, 12, 0, 0, TimeSpan.Zero);

    private readonly IReadingStatisticsService _readingStatisticsService = A.Fake<IReadingStatisticsService>();
    private readonly GraphQLTestServer _server;

    public ReadingStatisticsQueriesTests()
    {
        _server = new GraphQLTestServer(A.Fake<IMeterReadingsService>(), _readingStatisticsService);
    }

    public async ValueTask DisposeAsync()
    {
        await _server.DisposeAsync();
    }

    [Fact]
    public async Task EnergyStatistics_Arguments_ArePassedToService()
    {
        // Arrange
        A.CallTo(() => _readingStatisticsService.GetEnergyStatisticsAsync(A<GetEnergyStatisticsRequest>._, A<CancellationToken>._))
            .Returns(Result<IReadOnlyList<EnergyStatisticsModel>>.Succeed([]));

        // Act
        await _server.ExecuteAsync(
            """
            {
              energyStatistics(
                filter: { locations: [KITCHEN, OFFICE], from: "2026-11-04T14:00:00+02:00", to: "2026-11-04T13:00:00Z" }
                groupBy: [LOCATION, TIME]
                bucket: FIVE_MINUTES
              ) { samples }
            }
            """);

        // Assert
        A.CallTo(() => _readingStatisticsService.GetEnergyStatisticsAsync(
                A<GetEnergyStatisticsRequest>.That.Matches(r =>
                    r.Locations!.SequenceEqual(new[] { MeterLocation.Kitchen, MeterLocation.Office })
                    && r.From == From
                    && r.To == From.AddHours(1)
                    && r.GroupBy.SequenceEqual(new[] { StatisticsGrouping.Location, StatisticsGrouping.Time })
                    && r.Bucket == TimeBucket.FiveMinutes),
                A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task EnergyStatistics_ServiceReturnsStatistics_ReturnsThemWithUngroupedFieldsNull()
    {
        // Arrange
        A.CallTo(() => _readingStatisticsService.GetEnergyStatisticsAsync(A<GetEnergyStatisticsRequest>._, A<CancellationToken>._))
            .Returns(Result<IReadOnlyList<EnergyStatisticsModel>>.Succeed(
            [
                new EnergyStatisticsModel
                {
                    Location = MeterLocation.Garage,
                    Samples = 4,
                    Energy = new MetricStatisticsModel { Average = 2.5, Minimum = 1.25, Maximum = 4 }
                }
            ]));

        // Act
        var response = await _server.ExecuteAsync(
            """
            {
              energyStatistics(filter: { from: "2026-11-04T12:00:00Z", to: "2026-11-04T13:00:00Z" }, groupBy: [LOCATION]) {
                location bucketStart samples energy { average minimum maximum }
              }
            }
            """);

        // Assert
        var statistics = response.GetProperty("data").GetProperty("energyStatistics").EnumerateArray().ShouldHaveSingleItem();
        statistics.GetProperty("location").GetString().ShouldBe("GARAGE");
        statistics.GetProperty("bucketStart").ValueKind.ShouldBe(JsonValueKind.Null);
        statistics.GetProperty("samples").GetInt32().ShouldBe(4);
        ShouldBeMetric(statistics.GetProperty("energy"), 2.5, 1.25, 4);
    }

    [Fact]
    public async Task AirQualityStatistics_ServiceReturnsStatistics_ReturnsStatisticsOfEachMetric()
    {
        // Arrange
        A.CallTo(() => _readingStatisticsService.GetAirQualityStatisticsAsync(A<GetAirQualityStatisticsRequest>._, A<CancellationToken>._))
            .Returns(Result<IReadOnlyList<AirQualityStatisticsModel>>.Succeed(
            [
                new AirQualityStatisticsModel
                {
                    BucketStart = From,
                    Samples = 2,
                    Co2 = new MetricStatisticsModel { Average = 700, Minimum = 600, Maximum = 800 },
                    Pm25 = new MetricStatisticsModel { Average = 20, Minimum = 10, Maximum = 30 },
                    Humidity = new MetricStatisticsModel { Average = 42.5, Minimum = 40, Maximum = 45 }
                }
            ]));

        // Act
        var response = await _server.ExecuteAsync(
            """
            {
              airQualityStatistics(
                filter: { locations: [BEDROOM], from: "2026-11-04T12:00:00Z", to: "2026-11-04T13:00:00Z" }
                groupBy: [TIME]
                bucket: HOUR
              ) {
                location bucketStart samples
                co2 { average minimum maximum }
                pm25 { average minimum maximum }
                humidity { average minimum maximum }
              }
            }
            """);

        // Assert
        var statistics = response.GetProperty("data").GetProperty("airQualityStatistics").EnumerateArray().ShouldHaveSingleItem();
        statistics.GetProperty("location").ValueKind.ShouldBe(JsonValueKind.Null);
        statistics.GetProperty("bucketStart").GetDateTimeOffset().ShouldBe(From);
        statistics.GetProperty("samples").GetInt32().ShouldBe(2);
        ShouldBeMetric(statistics.GetProperty("co2"), 700, 600, 800);
        ShouldBeMetric(statistics.GetProperty("pm25"), 20, 10, 30);
        ShouldBeMetric(statistics.GetProperty("humidity"), 42.5, 40, 45);

        A.CallTo(() => _readingStatisticsService.GetAirQualityStatisticsAsync(
                A<GetAirQualityStatisticsRequest>.That.Matches(r =>
                    r.Locations!.SequenceEqual(new[] { MeterLocation.Bedroom })
                    && r.From == From
                    && r.To == From.AddHours(1)
                    && r.GroupBy.SequenceEqual(new[] { StatisticsGrouping.Time })
                    && r.Bucket == TimeBucket.Hour),
                A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task MotionStatistics_ServiceReturnsStatistics_ReturnsMotionSamplesAndActivityRatio()
    {
        // Arrange
        A.CallTo(() => _readingStatisticsService.GetMotionStatisticsAsync(A<GetMotionStatisticsRequest>._, A<CancellationToken>._))
            .Returns(Result<IReadOnlyList<MotionStatisticsModel>>.Succeed(
            [
                new MotionStatisticsModel { Samples = 4, MotionSamples = 1 }
            ]));

        // Act
        var response = await _server.ExecuteAsync(
            """
            {
              motionStatistics(filter: { from: "2026-11-04T12:00:00Z", to: "2026-11-04T13:00:00Z" }, groupBy: []) {
                location bucketStart samples motionSamples activityRatio
              }
            }
            """);

        // Assert
        var statistics = response.GetProperty("data").GetProperty("motionStatistics").EnumerateArray().ShouldHaveSingleItem();
        statistics.GetProperty("location").ValueKind.ShouldBe(JsonValueKind.Null);
        statistics.GetProperty("bucketStart").ValueKind.ShouldBe(JsonValueKind.Null);
        statistics.GetProperty("samples").GetInt32().ShouldBe(4);
        statistics.GetProperty("motionSamples").GetInt32().ShouldBe(1);
        statistics.GetProperty("activityRatio").GetDouble().ShouldBe(0.25);

        A.CallTo(() => _readingStatisticsService.GetMotionStatisticsAsync(
                A<GetMotionStatisticsRequest>.That.Matches(r =>
                    r.Locations == null
                    && r.From == From
                    && r.To == From.AddHours(1)
                    && r.GroupBy.Count == 0
                    && r.Bucket == null),
                A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task EnergyStatistics_FailedResult_ReturnsBadRequestWithResultMessage()
    {
        // Arrange
        A.CallTo(() => _readingStatisticsService.GetEnergyStatisticsAsync(A<GetEnergyStatisticsRequest>._, A<CancellationToken>._))
            .Returns(Result<IReadOnlyList<EnergyStatisticsModel>>.Fail("'Bucket' is required when grouping by time."));

        // Act
        var response = await _server.ExecuteAsync(
            """{ energyStatistics(filter: { from: "2026-11-04T12:00:00Z", to: "2026-11-04T13:00:00Z" }, groupBy: [TIME]) { samples } }""");

        // Assert
        var error = response.GetProperty("errors").EnumerateArray().ShouldHaveSingleItem();
        error.GetProperty("message").GetString().ShouldBe("'Bucket' is required when grouping by time.");
        error.GetProperty("extensions").GetProperty("code").GetString().ShouldBe(GraphQLErrorCodes.BadRequest);
        error.GetProperty("path")[0].GetString().ShouldBe("energyStatistics");
    }

    [Theory]
    [InlineData("""{ energyStatistics(groupBy: []) { samples } }""")]
    [InlineData("""{ energyStatistics(filter: { from: "2026-11-04T12:00:00Z" }, groupBy: []) { samples } }""")]
    [InlineData("""{ energyStatistics(filter: { from: "2026-11-04T12:00:00Z", to: "2026-11-04T13:00:00Z" }) { samples } }""")]
    public async Task EnergyStatistics_RequiredArgumentMissing_ReturnsErrorWithoutCallingService(string query)
    {
        // Act
        var response = await _server.ExecuteAsync(query);

        // Assert
        response.GetProperty("errors").GetArrayLength().ShouldBeGreaterThan(0);
        A.CallTo(_readingStatisticsService).MustNotHaveHappened();
    }

    private static void ShouldBeMetric(JsonElement metric, double average, double minimum, double maximum)
    {
        metric.GetProperty("average").GetDouble().ShouldBe(average);
        metric.GetProperty("minimum").GetDouble().ShouldBe(minimum);
        metric.GetProperty("maximum").GetDouble().ShouldBe(maximum);
    }
}
