using System.Text.Json;
using ApiGateway.Common.Constants.Enums;
using ApiGateway.IntegrationTests.Fixtures;
using ApiGateway.IntegrationTests.Utilities;
using Shouldly;
using Xunit;

namespace ApiGateway.IntegrationTests.Api.GraphQL;

/// <summary>
/// The <c>motionStatistics</c> query against the real <c>reporting.meter_readings</c> view.
/// </summary>
public sealed class MotionStatisticsQueryTests(PostgreSqlContainerFixture postgreSql) : IAsyncLifetime
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
    public async Task MotionStatistics_ByLocationAndHour_CountsMotionSamplesAndActivityRatioPerGroup()
    {
        // Arrange
        await SeedAsync(
            SeedReadings.Motion(MeterLocation.Garage, HourStart, motionDetected: true),
            SeedReadings.Motion(MeterLocation.Garage, HourStart.AddMinutes(10), motionDetected: false),
            SeedReadings.Motion(MeterLocation.Garage, HourStart.AddMinutes(20), motionDetected: false),
            SeedReadings.Motion(MeterLocation.Garage, HourStart.AddMinutes(30), motionDetected: false),
            SeedReadings.Motion(MeterLocation.Garage, HourStart.AddMinutes(60), motionDetected: true),
            SeedReadings.Motion(MeterLocation.Corridor, HourStart, motionDetected: false),
            SeedReadings.Energy(MeterLocation.Garage, HourStart));

        // Act
        var statistics = await GetStatisticsAsync(
            """
            {
              motionStatistics(
                filter: { from: "2026-11-01T12:00:00Z", to: "2026-11-01T14:00:00Z" }
                groupBy: [LOCATION, TIME]
                bucket: HOUR
              ) { location bucketStart samples motionSamples activityRatio }
            }
            """);

        // Assert
        statistics.ShouldBe(
        [
            new MotionStatisticsRow("GARAGE", HourStart, 4, 1, 0.25),
            new MotionStatisticsRow("GARAGE", HourStart.AddHours(1), 1, 1, 1),
            new MotionStatisticsRow("CORRIDOR", HourStart, 1, 0, 0)
        ]);
    }

    [Fact]
    public async Task MotionStatistics_NoGrouping_CountsMotionReadingsOfTheWholeRange()
    {
        // Arrange
        await SeedAsync(
            SeedReadings.Motion(MeterLocation.Kitchen, HourStart, motionDetected: true),
            SeedReadings.Motion(MeterLocation.Office, HourStart, motionDetected: false),
            SeedReadings.AirQuality(MeterLocation.Kitchen, HourStart));

        // Act
        var statistics = await GetStatisticsAsync(
            """
            {
              motionStatistics(filter: { from: "2026-11-01T12:00:00Z", to: "2026-11-01T13:00:00Z" }, groupBy: []) {
                location bucketStart samples motionSamples activityRatio
              }
            }
            """);

        // Assert
        statistics.ShouldBe([new MotionStatisticsRow(null, null, 2, 1, 0.5)]);
    }

    private static DateTimeOffset? GetNullableDateTimeOffset(JsonElement element)
    {
        return element.ValueKind == JsonValueKind.Null ? null : element.GetDateTimeOffset();
    }

    private async Task SeedAsync(params SeedReading[] readings)
    {
        await MeterReadingsTable.InsertAsync(postgreSql, readings, TestContext.Current.CancellationToken);
    }

    private async Task<List<MotionStatisticsRow>> GetStatisticsAsync(string query)
    {
        var response = await new GraphQLClient(_factory.CreateClient()).ExecuteAsync(query);

        response.Errors.ShouldBeEmpty();

        return
        [
            .. response.Data.GetProperty("motionStatistics").EnumerateArray().Select(statistics => new MotionStatisticsRow(
                statistics.GetProperty("location").GetString(),
                GetNullableDateTimeOffset(statistics.GetProperty("bucketStart")),
                statistics.GetProperty("samples").GetInt32(),
                statistics.GetProperty("motionSamples").GetInt32(),
                statistics.GetProperty("activityRatio").GetDouble()))
        ];
    }

    private sealed record MotionStatisticsRow(
        string? Location,
        DateTimeOffset? BucketStart,
        int Samples,
        int MotionSamples,
        double ActivityRatio);
}
