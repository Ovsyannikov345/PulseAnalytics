using System.Text.Json;
using ApiGateway.Common.Constants.Enums;
using ApiGateway.IntegrationTests.Fixtures;
using ApiGateway.IntegrationTests.Utilities;
using Shouldly;
using Xunit;

namespace ApiGateway.IntegrationTests.Api.GraphQL;

/// <summary>
/// The <c>latestReadings</c> query against the real <c>reporting.meter_readings</c> view.
/// </summary>
public sealed class LatestReadingsQueryTests(PostgreSqlContainerFixture postgreSql) : IAsyncLifetime
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
    public async Task LatestReadings_SeveralReadingsPerMeter_ReturnsNewestPerMeterOrderedByTypeAndLocation()
    {
        // Arrange
        var newestKitchenEnergy = SeedReadings.Energy(MeterLocation.Kitchen, ReceivedAt.AddMinutes(2));
        var onlyGarageEnergy = SeedReadings.Energy(MeterLocation.Garage, ReceivedAt.AddMinutes(1));
        var newestBedroomMotion = SeedReadings.Motion(MeterLocation.Bedroom, ReceivedAt.AddMinutes(3));
        var newestOfficeAirQuality = SeedReadings.AirQuality(MeterLocation.Office, ReceivedAt.AddMinutes(5));

        await SeedAsync(
            SeedReadings.Energy(MeterLocation.Kitchen, ReceivedAt),
            newestKitchenEnergy,
            onlyGarageEnergy,
            SeedReadings.Motion(MeterLocation.Bedroom, ReceivedAt),
            newestBedroomMotion,
            SeedReadings.AirQuality(MeterLocation.Office, ReceivedAt.AddMinutes(1)),
            newestOfficeAirQuality);

        // Act
        var response = await CreateClient().ExecuteAsync("{ latestReadings { id } }");

        // Assert
        response.Errors.ShouldBeEmpty();

        // Meters without readings are left out.
        GetIds(response.Data.GetProperty("latestReadings")).ShouldBe(
        [
            newestOfficeAirQuality.Id,
            newestKitchenEnergy.Id,
            onlyGarageEnergy.Id,
            newestBedroomMotion.Id
        ]);
    }

    [Fact]
    public async Task LatestReadings_FilterByTypesAndLocations_ReturnsSelectedMetersOnly()
    {
        // Arrange
        var kitchenEnergy = SeedReadings.Energy(MeterLocation.Kitchen, ReceivedAt);
        var kitchenMotion = SeedReadings.Motion(MeterLocation.Kitchen, ReceivedAt);

        await SeedAsync(
            kitchenEnergy,
            kitchenMotion,
            SeedReadings.Energy(MeterLocation.Office, ReceivedAt),
            SeedReadings.AirQuality(MeterLocation.Kitchen, ReceivedAt));

        // Act
        var response = await CreateClient().ExecuteAsync(
            "{ latestReadings(filter: { types: [ENERGY, MOTION], locations: [KITCHEN] }) { id } }");

        // Assert
        response.Errors.ShouldBeEmpty();

        GetIds(response.Data.GetProperty("latestReadings")).ShouldBe([kitchenEnergy.Id, kitchenMotion.Id]);
    }

    private static List<Guid> GetIds(JsonElement readings)
    {
        return [.. readings.EnumerateArray().Select(reading => reading.GetProperty("id").GetGuid())];
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
