using System.Text.Json;
using ApiGateway.Common.Constants.Enums;
using ApiGateway.IntegrationTests.Fixtures;
using ApiGateway.IntegrationTests.Utilities;
using Shouldly;
using Xunit;

namespace ApiGateway.IntegrationTests.Api.GraphQL;

/// <summary>
/// The <c>energyStatistics</c> query against the real <c>reporting.meter_readings</c> view: the grouping, the date_bin
/// buckets on the UTC grid and the aggregates as PostgreSQL computes them.
/// </summary>
public sealed class EnergyStatisticsQueryTests(PostgreSqlContainerFixture postgreSql) : IAsyncLifetime
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
    public async Task EnergyStatistics_ByLocationAndHour_ReturnsStatisticsPerGroupOrderedByLocationAndBucket()
    {
        // Arrange
        await SeedAsync(
            SeedReadings.Energy(MeterLocation.Office, HourStart.AddMinutes(15), energy: 6),
            SeedReadings.Energy(MeterLocation.Kitchen, HourStart, energy: 2),
            SeedReadings.Energy(MeterLocation.Kitchen, HourStart.AddMinutes(30), energy: 4),
            SeedReadings.Energy(MeterLocation.Kitchen, HourStart.AddMinutes(70), energy: 6.5));

        // Act
        var statistics = await GetStatisticsAsync(
            """
            {
              energyStatistics(
                filter: { from: "2026-11-01T12:00:00Z", to: "2026-11-01T14:00:00Z" }
                groupBy: [LOCATION, TIME]
                bucket: HOUR
              ) { location bucketStart samples energy { average minimum maximum } }
            }
            """);

        // Assert
        statistics.ShouldBe(
        [
            new EnergyStatisticsRow("KITCHEN", HourStart, 2, 3, 2, 4),
            new EnergyStatisticsRow("KITCHEN", HourStart.AddHours(1), 1, 6.5, 6.5, 6.5),
            new EnergyStatisticsRow("OFFICE", HourStart, 1, 6, 6, 6)
        ]);
    }

    [Fact]
    public async Task EnergyStatistics_ReadingsAtBucketEdges_FallIntoTheBucketTheyStart()
    {
        // Arrange
        await SeedAsync(
            SeedReadings.Energy(MeterLocation.Kitchen, HourStart.AddMinutes(59).AddSeconds(59), energy: 1),
            SeedReadings.Energy(MeterLocation.Kitchen, HourStart.AddHours(1), energy: 2),
            SeedReadings.Energy(MeterLocation.Kitchen, HourStart.AddHours(2), energy: 100));

        // Act
        var statistics = await GetStatisticsAsync(
            """
            {
              energyStatistics(
                filter: { from: "2026-11-01T12:00:00Z", to: "2026-11-01T14:00:00Z" }
                groupBy: [TIME]
                bucket: HOUR
              ) { location bucketStart samples energy { average minimum maximum } }
            }
            """);

        // Assert
        // The reading at 'to' (14:00) is outside [from, to).
        statistics.ShouldBe(
        [
            new EnergyStatisticsRow(null, HourStart, 1, 1, 1, 1),
            new EnergyStatisticsRow(null, HourStart.AddHours(1), 1, 2, 2, 2)
        ]);
    }

    [Fact]
    public async Task EnergyStatistics_HourWithoutReadings_IsLeftOut()
    {
        // Arrange
        await SeedAsync(
            SeedReadings.Energy(MeterLocation.Kitchen, HourStart, energy: 1),
            SeedReadings.Energy(MeterLocation.Kitchen, HourStart.AddHours(2), energy: 3));

        // Act
        var statistics = await GetStatisticsAsync(
            """
            {
              energyStatistics(
                filter: { from: "2026-11-01T12:00:00Z", to: "2026-11-01T15:00:00Z" }
                groupBy: [TIME]
                bucket: HOUR
              ) { location bucketStart samples energy { average minimum maximum } }
            }
            """);

        // Assert
        statistics.Select(s => s.BucketStart).ShouldBe([HourStart, HourStart.AddHours(2)]);
    }

    [Fact]
    public async Task EnergyStatistics_DayBuckets_StartAtUtcMidnight()
    {
        // Arrange
        // 23:30 at UTC+2 is 21:30 UTC; 00:30 at UTC+2 on the next day is still 1 November in UTC.
        var plusTwo = TimeSpan.FromHours(2);
        await SeedAsync(
            SeedReadings.Energy(MeterLocation.Kitchen, new DateTimeOffset(2026, 11, 1, 23, 30, 0, plusTwo), energy: 1),
            SeedReadings.Energy(MeterLocation.Kitchen, new DateTimeOffset(2026, 11, 2, 0, 30, 0, plusTwo), energy: 3),
            SeedReadings.Energy(MeterLocation.Kitchen, new DateTimeOffset(2026, 11, 2, 2, 30, 0, plusTwo), energy: 5));

        // Act
        var statistics = await GetStatisticsAsync(
            """
            {
              energyStatistics(
                filter: { from: "2026-11-01T06:00:00Z", to: "2026-11-03T00:00:00Z" }
                groupBy: [TIME]
                bucket: DAY
              ) { location bucketStart samples energy { average minimum maximum } }
            }
            """);

        // Assert
        statistics.ShouldBe(
        [
            new EnergyStatisticsRow(null, new DateTimeOffset(2026, 11, 1, 0, 0, 0, TimeSpan.Zero), 2, 2, 1, 3),
            new EnergyStatisticsRow(null, new DateTimeOffset(2026, 11, 2, 0, 0, 0, TimeSpan.Zero), 1, 5, 5, 5)
        ]);
    }

    [Fact]
    public async Task EnergyStatistics_FiveMinuteBuckets_AlignWithGridNotWithFrom()
    {
        // Arrange
        await SeedAsync(
            SeedReadings.Energy(MeterLocation.Kitchen, HourStart.AddMinutes(3), energy: 1),
            SeedReadings.Energy(MeterLocation.Kitchen, HourStart.AddMinutes(7), energy: 2));

        // Act
        var statistics = await GetStatisticsAsync(
            """
            {
              energyStatistics(
                filter: { from: "2026-11-01T12:02:00Z", to: "2026-11-01T12:10:00Z" }
                groupBy: [TIME]
                bucket: FIVE_MINUTES
              ) { location bucketStart samples energy { average minimum maximum } }
            }
            """);

        // Assert
        statistics.Select(s => s.BucketStart).ShouldBe([HourStart, HourStart.AddMinutes(5)]);
    }

    [Fact]
    public async Task EnergyStatistics_NoGrouping_ReturnsOneRowForTheWholeRange()
    {
        // Arrange
        await SeedAsync(
            SeedReadings.Energy(MeterLocation.Kitchen, HourStart, energy: 2),
            SeedReadings.Energy(MeterLocation.Office, HourStart.AddMinutes(30), energy: 7));

        // Act
        var statistics = await GetStatisticsAsync(
            """
            {
              energyStatistics(filter: { from: "2026-11-01T12:00:00Z", to: "2026-11-01T13:00:00Z" }, groupBy: []) {
                location bucketStart samples energy { average minimum maximum }
              }
            }
            """);

        // Assert
        statistics.ShouldBe([new EnergyStatisticsRow(null, null, 2, 4.5, 2, 7)]);
    }

    [Theory]
    [InlineData("groupBy: []")]
    [InlineData("groupBy: [LOCATION]")]
    [InlineData("groupBy: [LOCATION, TIME], bucket: MINUTE")]
    public async Task EnergyStatistics_NoReadingsInRange_ReturnsEmptyList(string grouping)
    {
        // Arrange
        await SeedAsync(SeedReadings.Energy(MeterLocation.Kitchen, HourStart.AddHours(-1)));

        // Act
        var statistics = await GetStatisticsAsync(
            $$"""
            {
              energyStatistics(filter: { from: "2026-11-01T12:00:00Z", to: "2026-11-01T13:00:00Z" }, {{grouping}}) {
                location bucketStart samples energy { average minimum maximum }
              }
            }
            """);

        // Assert
        statistics.ShouldBeEmpty();
    }

    [Fact]
    public async Task EnergyStatistics_LocationFilter_AggregatesEnergyReadingsOfSelectedLocationsOnly()
    {
        // Arrange
        await SeedAsync(
            SeedReadings.Energy(MeterLocation.Kitchen, HourStart, energy: 2),
            SeedReadings.Energy(MeterLocation.Garage, HourStart, energy: 4),
            SeedReadings.Energy(MeterLocation.Office, HourStart, energy: 100),
            SeedReadings.Motion(MeterLocation.Kitchen, HourStart),
            SeedReadings.AirQuality(MeterLocation.Garage, HourStart));

        // Act
        var statistics = await GetStatisticsAsync(
            """
            {
              energyStatistics(
                filter: { locations: [KITCHEN, GARAGE], from: "2026-11-01T12:00:00Z", to: "2026-11-01T13:00:00Z" }
                groupBy: []
              ) { location bucketStart samples energy { average minimum maximum } }
            }
            """);

        // Assert
        statistics.ShouldBe([new EnergyStatisticsRow(null, null, 2, 3, 2, 4)]);
    }

    private static DateTimeOffset? GetNullableDateTimeOffset(JsonElement element)
    {
        return element.ValueKind == JsonValueKind.Null ? null : element.GetDateTimeOffset();
    }

    private async Task SeedAsync(params SeedReading[] readings)
    {
        await MeterReadingsTable.InsertAsync(postgreSql, readings, TestContext.Current.CancellationToken);
    }

    private async Task<List<EnergyStatisticsRow>> GetStatisticsAsync(string query)
    {
        var response = await new GraphQLClient(_factory.CreateClient()).ExecuteAsync(query);

        response.Errors.ShouldBeEmpty();

        return
        [
            .. response.Data.GetProperty("energyStatistics").EnumerateArray().Select(statistics => new EnergyStatisticsRow(
                statistics.GetProperty("location").GetString(),
                GetNullableDateTimeOffset(statistics.GetProperty("bucketStart")),
                statistics.GetProperty("samples").GetInt32(),
                statistics.GetProperty("energy").GetProperty("average").GetDouble(),
                statistics.GetProperty("energy").GetProperty("minimum").GetDouble(),
                statistics.GetProperty("energy").GetProperty("maximum").GetDouble()))
        ];
    }

    private sealed record EnergyStatisticsRow(
        string? Location,
        DateTimeOffset? BucketStart,
        int Samples,
        double Average,
        double Minimum,
        double Maximum);
}
