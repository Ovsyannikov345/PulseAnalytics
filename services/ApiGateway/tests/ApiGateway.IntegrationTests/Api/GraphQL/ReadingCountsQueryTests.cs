using ApiGateway.Common.Constants.Enums;
using ApiGateway.IntegrationTests.Fixtures;
using ApiGateway.IntegrationTests.Utilities;
using Shouldly;
using Xunit;

namespace ApiGateway.IntegrationTests.Api.GraphQL;

/// <summary>
/// The <c>readingCounts</c> query against the real <c>reporting.meter_readings</c> view.
/// </summary>
public sealed class ReadingCountsQueryTests(PostgreSqlContainerFixture postgreSql) : IAsyncLifetime
{
    private static readonly DateTimeOffset ReceivedAt = new(2026, 11, 1, 12, 0, 0, TimeSpan.Zero);

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
    public async Task ReadingCounts_GroupByTypeAndLocation_ReturnsCountPerMeterOrderedByTypeAndLocation()
    {
        // Arrange
        await SeedReadingsAsync();

        // Act
        var counts = await GetCountsAsync("{ readingCounts(groupBy: [TYPE, LOCATION]) { type location count } }");

        // Assert
        counts.ShouldBe(
        [
            new ReadingCountRow("AIR_QUALITY", "OFFICE", 1),
            new ReadingCountRow("ENERGY", "KITCHEN", 3),
            new ReadingCountRow("ENERGY", "OFFICE", 1),
            new ReadingCountRow("MOTION", "KITCHEN", 2)
        ]);
    }

    [Fact]
    public async Task ReadingCounts_GroupByType_ReturnsCountPerTypeWithoutLocation()
    {
        // Arrange
        await SeedReadingsAsync();

        // Act
        var counts = await GetCountsAsync("{ readingCounts(groupBy: [TYPE]) { type location count } }");

        // Assert
        counts.ShouldBe(
        [
            new ReadingCountRow("AIR_QUALITY", null, 1),
            new ReadingCountRow("ENERGY", null, 4),
            new ReadingCountRow("MOTION", null, 2)
        ]);
    }

    [Fact]
    public async Task ReadingCounts_GroupByLocation_ReturnsCountPerLocationWithoutType()
    {
        // Arrange
        await SeedReadingsAsync();

        // Act
        var counts = await GetCountsAsync("{ readingCounts(groupBy: [LOCATION]) { type location count } }");

        // Assert
        counts.ShouldBe(
        [
            new ReadingCountRow(null, "KITCHEN", 5),
            new ReadingCountRow(null, "OFFICE", 2)
        ]);
    }

    [Fact]
    public async Task ReadingCounts_EmptyGroupBy_ReturnsTotalCount()
    {
        // Arrange
        await SeedReadingsAsync();

        // Act
        var counts = await GetCountsAsync("{ readingCounts(groupBy: []) { type location count } }");

        // Assert
        counts.ShouldBe([new ReadingCountRow(null, null, 7)]);
    }

    [Fact]
    public async Task ReadingCounts_NoReadings_ReturnsZeroTotalCount()
    {
        // Act
        var counts = await GetCountsAsync("{ readingCounts(groupBy: []) { type location count } }");

        // Assert
        counts.ShouldBe([new ReadingCountRow(null, null, 0)]);
    }

    [Fact]
    public async Task ReadingCounts_Filter_CountsMatchingReadingsOnly()
    {
        // Arrange
        await SeedReadingsAsync();

        // Act
        var counts = await GetCountsAsync(
            """
            {
              readingCounts(
                filter: { types: [ENERGY], from: "2026-11-01T12:01:00Z", to: "2026-11-01T12:10:00Z" }
                groupBy: [LOCATION]
              ) { type location count }
            }
            """);

        // Assert
        counts.ShouldBe([new ReadingCountRow(null, "KITCHEN", 2)]);
    }

    /// <summary>
    /// Kitchen: 3 energy, 2 motion. Office: 1 energy, 1 air quality. 7 in total.
    /// </summary>
    private async Task SeedReadingsAsync()
    {
        SeedReading[] readings =
        [
            SeedReadings.Energy(MeterLocation.Kitchen, ReceivedAt),
            SeedReadings.Energy(MeterLocation.Kitchen, ReceivedAt.AddMinutes(1)),
            SeedReadings.Energy(MeterLocation.Kitchen, ReceivedAt.AddMinutes(2)),
            SeedReadings.Energy(MeterLocation.Office, ReceivedAt),
            SeedReadings.Motion(MeterLocation.Kitchen, ReceivedAt),
            SeedReadings.Motion(MeterLocation.Kitchen, ReceivedAt.AddMinutes(1)),
            SeedReadings.AirQuality(MeterLocation.Office, ReceivedAt.AddMinutes(5))
        ];

        await MeterReadingsTable.InsertAsync(postgreSql, readings, TestContext.Current.CancellationToken);
    }

    private async Task<List<ReadingCountRow>> GetCountsAsync(string query)
    {
        var response = await new GraphQLClient(_factory.CreateClient()).ExecuteAsync(query);

        response.Errors.ShouldBeEmpty();

        return
        [
            .. response.Data.GetProperty("readingCounts").EnumerateArray().Select(count => new ReadingCountRow(
                count.GetProperty("type").GetString(),
                count.GetProperty("location").GetString(),
                count.GetProperty("count").GetInt32()))
        ];
    }

    private sealed record ReadingCountRow(string? Type, string? Location, int Count);
}
