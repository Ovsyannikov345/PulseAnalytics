using System.Text.Json;
using ApiGateway.Common.Constants.Enums;
using ApiGateway.IntegrationTests.Fixtures;
using ApiGateway.IntegrationTests.Utilities;
using Shouldly;
using Xunit;

namespace ApiGateway.IntegrationTests.Api.GraphQL;

/// <summary>
/// The <c>airQualityStatistics</c> query against the real <c>reporting.meter_readings</c> view.
/// </summary>
public sealed class AirQualityStatisticsQueryTests(PostgreSqlContainerFixture postgreSql) : IAsyncLifetime
{
    private static readonly DateTimeOffset HourStart = new(2026, 11, 1, 12, 0, 0, TimeSpan.Zero);

    private readonly ApiGatewayApplicationFactory _factory = new(postgreSql);

    public async ValueTask InitializeAsync()
    {
        await MeterReadingsTable.ClearAsync(postgreSql, TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _factory.DisposeAsync();
    }

    [Fact]
    public async Task AirQualityStatistics_ByLocation_ReturnsStatisticsOfEachMetricForAirQualityReadingsOnly()
    {
        // Arrange
        await SeedAsync(
            SeedReadings.AirQuality(MeterLocation.Kitchen, HourStart, co2: 600, pm25: 10, humidity: 40),
            SeedReadings.AirQuality(MeterLocation.Kitchen, HourStart.AddMinutes(1), co2: 800, pm25: 30, humidity: 45),
            SeedReadings.AirQuality(MeterLocation.Bedroom, HourStart, co2: 500, pm25: 5, humidity: 50),
            SeedReadings.Energy(MeterLocation.Kitchen, HourStart),
            SeedReadings.Motion(MeterLocation.Bedroom, HourStart));

        // Act
        var response = await CreateClient().ExecuteAsync(
            """
            {
              airQualityStatistics(
                filter: { from: "2026-11-01T12:00:00Z", to: "2026-11-01T13:00:00Z" }
                groupBy: [LOCATION]
              ) {
                location bucketStart samples
                co2 { average minimum maximum }
                pm25 { average minimum maximum }
                humidity { average minimum maximum }
              }
            }
            """);

        // Assert
        response.Errors.ShouldBeEmpty();

        var statistics = response.Data.GetProperty("airQualityStatistics").EnumerateArray().ToList();
        statistics.Count.ShouldBe(2);

        statistics[0].GetProperty("location").GetString().ShouldBe("KITCHEN");
        statistics[0].GetProperty("bucketStart").ValueKind.ShouldBe(JsonValueKind.Null);
        statistics[0].GetProperty("samples").GetInt32().ShouldBe(2);
        ShouldBeMetric(statistics[0].GetProperty("co2"), 700, 600, 800);
        ShouldBeMetric(statistics[0].GetProperty("pm25"), 20, 10, 30);
        ShouldBeMetric(statistics[0].GetProperty("humidity"), 42.5, 40, 45);

        statistics[1].GetProperty("location").GetString().ShouldBe("BEDROOM");
        statistics[1].GetProperty("samples").GetInt32().ShouldBe(1);
        ShouldBeMetric(statistics[1].GetProperty("co2"), 500, 500, 500);
        ShouldBeMetric(statistics[1].GetProperty("pm25"), 5, 5, 5);
        ShouldBeMetric(statistics[1].GetProperty("humidity"), 50, 50, 50);
    }

    [Fact]
    public async Task AirQualityStatistics_ByFifteenMinutes_ReturnsStatisticsPerBucket()
    {
        // Arrange
        await SeedAsync(
            SeedReadings.AirQuality(MeterLocation.Office, HourStart.AddMinutes(14).AddSeconds(59), co2: 400),
            SeedReadings.AirQuality(MeterLocation.Office, HourStart.AddMinutes(15), co2: 900));

        // Act
        var response = await CreateClient().ExecuteAsync(
            """
            {
              airQualityStatistics(
                filter: { from: "2026-11-01T12:00:00Z", to: "2026-11-01T13:00:00Z" }
                groupBy: [TIME]
                bucket: FIFTEEN_MINUTES
              ) { bucketStart co2 { average } }
            }
            """);

        // Assert
        response.Errors.ShouldBeEmpty();

        var statistics = response.Data.GetProperty("airQualityStatistics").EnumerateArray().ToList();
        statistics.Select(s => s.GetProperty("bucketStart").GetDateTimeOffset()).ShouldBe([HourStart, HourStart.AddMinutes(15)]);
        statistics.Select(s => s.GetProperty("co2").GetProperty("average").GetDouble()).ShouldBe([400, 900]);
    }

    private static void ShouldBeMetric(JsonElement metric, double average, double minimum, double maximum)
    {
        metric.GetProperty("average").GetDouble().ShouldBe(average);
        metric.GetProperty("minimum").GetDouble().ShouldBe(minimum);
        metric.GetProperty("maximum").GetDouble().ShouldBe(maximum);
    }

    private GraphQLClient CreateClient()
    {
        return new GraphQLClient(_factory.CreateClient());
    }

    private async Task SeedAsync(params SeedReading[] readings)
    {
        await MeterReadingsTable.InsertAsync(postgreSql, readings, TestContext.Current.CancellationToken);
    }
}
