using System.Text.Json;
using ApiGateway.Common.Constants.Enums;
using ApiGateway.IntegrationTests.Fixtures;
using ApiGateway.IntegrationTests.Utilities;
using Shouldly;
using Xunit;

namespace ApiGateway.IntegrationTests.Api.GraphQL;

/// <summary>
/// The <c>readings</c> query against the real <c>reporting.meter_readings</c> view.
/// </summary>
public sealed class ReadingsQueryTests(PostgreSqlContainerFixture postgreSql) : IAsyncLifetime
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
    public async Task Readings_OneReadingOfEachType_ReturnsTypedNodesWithOwnValues()
    {
        // Arrange
        var motion = SeedReadings.Motion(MeterLocation.Garage, ReceivedAt, motionDetected: true);
        var energy = SeedReadings.Energy(MeterLocation.Office, ReceivedAt.AddSeconds(1), energy: 845.62);
        var airQuality = SeedReadings.AirQuality(MeterLocation.LivingRoom, ReceivedAt.AddSeconds(2), co2: 803, pm25: 25, humidity: 36);

        await SeedAsync(motion, energy, airQuality);

        // Act
        var response = await CreateClient().ExecuteAsync(
            """
            {
              readings {
                nodes {
                  __typename id type location receivedAt
                  ... on MotionReading { motionDetected }
                  ... on EnergyReading { energy }
                  ... on AirQualityReading { co2 pm25 humidity }
                }
              }
            }
            """);

        // Assert
        response.Errors.ShouldBeEmpty();

        var nodes = response.Data.GetProperty("readings").GetProperty("nodes").EnumerateArray().ToList();
        nodes.Count.ShouldBe(3);

        ShouldBeReading(nodes[0], "AirQualityReading", airQuality, "AIR_QUALITY", "LIVING_ROOM");
        nodes[0].GetProperty("co2").GetInt32().ShouldBe(803);
        nodes[0].GetProperty("pm25").GetInt32().ShouldBe(25);
        nodes[0].GetProperty("humidity").GetInt32().ShouldBe(36);

        ShouldBeReading(nodes[1], "EnergyReading", energy, "ENERGY", "OFFICE");
        nodes[1].GetProperty("energy").GetDouble().ShouldBe(845.62);

        ShouldBeReading(nodes[2], "MotionReading", motion, "MOTION", "GARAGE");
        nodes[2].GetProperty("motionDetected").GetBoolean().ShouldBeTrue();
    }

    [Fact]
    public async Task Readings_FilterByTypesLocationsAndRange_ReturnsMatchingReadingsOnly()
    {
        // Arrange
        var from = ReceivedAt;
        var to = ReceivedAt.AddMinutes(10);

        var atFrom = SeedReadings.Energy(MeterLocation.Kitchen, from);
        var inRange = SeedReadings.Energy(MeterLocation.Kitchen, from.AddMinutes(5));
        var otherSelectedType = SeedReadings.AirQuality(MeterLocation.Bedroom, from.AddMinutes(1));

        await SeedAsync(
            atFrom,
            inRange,
            otherSelectedType,
            SeedReadings.Energy(MeterLocation.Kitchen, to),
            SeedReadings.Energy(MeterLocation.Kitchen, from.AddSeconds(-1)),
            SeedReadings.Energy(MeterLocation.Office, from.AddMinutes(5)),
            SeedReadings.Motion(MeterLocation.Kitchen, from.AddMinutes(5)));

        // Act
        var response = await CreateClient().ExecuteAsync(
            """
            query ($from: DateTime!, $to: DateTime!) {
              readings(filter: { types: [ENERGY, AIR_QUALITY], locations: [KITCHEN, BEDROOM], from: $from, to: $to }) {
                nodes { id }
              }
            }
            """,
            new Dictionary<string, object?> { ["from"] = from, ["to"] = to });

        // Assert
        response.Errors.ShouldBeEmpty();

        GetIds(response.Data.GetProperty("readings").GetProperty("nodes"))
            .ShouldBe([inRange.Id, otherSelectedType.Id, atFrom.Id]);
    }

    [Fact]
    public async Task Readings_SeveralPages_ReturnsEveryReadingOnceNewestFirst()
    {
        // Arrange
        // Three polls; the readings of a poll share their receive time, so the id decides their order.
        var firstPoll = ReceivedAt;
        var secondPoll = ReceivedAt.AddMinutes(1);
        var thirdPoll = ReceivedAt.AddMinutes(2);

        await SeedAsync(
            SeedReadings.Energy(MeterLocation.Kitchen, secondPoll, id: Id(5)),
            SeedReadings.Motion(MeterLocation.Garage, firstPoll, id: Id(1)),
            SeedReadings.AirQuality(MeterLocation.Office, thirdPoll, id: Id(8)),
            SeedReadings.Energy(MeterLocation.Kitchen, firstPoll, id: Id(3)),
            SeedReadings.Motion(MeterLocation.Garage, secondPoll, id: Id(4)),
            SeedReadings.AirQuality(MeterLocation.Office, firstPoll, id: Id(2)),
            SeedReadings.Energy(MeterLocation.Kitchen, thirdPoll, id: Id(7)),
            SeedReadings.AirQuality(MeterLocation.Office, secondPoll, id: Id(6)));

        var client = CreateClient();
        var pages = new List<JsonElement>();
        string? after = null;

        // Act
        do
        {
            var response = await client.ExecuteAsync(
                """
                query ($after: String) {
                  readings(first: 3, after: $after) {
                    nodes { id }
                    pageInfo { hasNextPage hasPreviousPage endCursor }
                  }
                }
                """,
                new Dictionary<string, object?> { ["after"] = after });

            response.Errors.ShouldBeEmpty();

            var page = response.Data.GetProperty("readings");
            pages.Add(page);
            after = page.GetProperty("pageInfo").GetProperty("endCursor").GetString();
        }
        while (pages[^1].GetProperty("pageInfo").GetProperty("hasNextPage").GetBoolean() && pages.Count < 10);

        // Assert
        pages.Select(page => GetIds(page.GetProperty("nodes"))).ShouldBe(
        [
            [Id(8), Id(7), Id(6)],
            [Id(5), Id(4), Id(3)],
            [Id(2), Id(1)]
        ]);

        pages.Select(page => page.GetProperty("pageInfo").GetProperty("hasPreviousPage").GetBoolean())
            .ShouldBe([false, true, true]);
    }

    private static Guid Id(int number)
    {
        return Guid.Parse($"00000000-0000-7000-8000-{number:D12}");
    }

    private static List<Guid> GetIds(JsonElement nodes)
    {
        return [.. nodes.EnumerateArray().Select(node => node.GetProperty("id").GetGuid())];
    }

    private static void ShouldBeReading(JsonElement node, string typeName, SeedReading reading, string type, string location)
    {
        node.GetProperty("__typename").GetString().ShouldBe(typeName);
        node.GetProperty("id").GetGuid().ShouldBe(reading.Id);
        node.GetProperty("type").GetString().ShouldBe(type);
        node.GetProperty("location").GetString().ShouldBe(location);
        node.GetProperty("receivedAt").GetDateTimeOffset().ShouldBe(reading.ReceivedAt);
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
