using System.Diagnostics.Metrics;
using System.Globalization;
using System.Net;
using System.Text.Json;
using ApiGateway.Common.Constants.Enums;
using ApiGateway.IntegrationTests.Fixtures;
using ApiGateway.IntegrationTests.Utilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.Metrics.Testing;
using Shouldly;
using Xunit;

namespace ApiGateway.IntegrationTests.Api.Rest;

/// <summary>
/// <c>GET /api/readings/export</c> against the real <c>reporting.meter_readings</c> view.
/// </summary>
public sealed class ReadingsExportTests(PostgreSqlContainerFixture postgreSql) : IAsyncLifetime
{
    private const string Header = "id,type,location,received_at,motion_detected,energy,co2,pm25,humidity";

    private const string MeterName = "PulseAnalytics.ApiGateway";

    private static readonly DateTimeOffset From = new(2026, 11, 1, 12, 0, 0, TimeSpan.Zero);

    private readonly List<ApiGatewayApplicationFactory> _factories = [];

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        await MeterReadingsTable.ClearAsync(postgreSql, Ct);
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var factory in _factories)
        {
            await factory.DisposeAsync();
        }
    }

    [Fact]
    public async Task Export_OneReadingOfEachType_ReturnsCsvFileWithOnlyEachTypesOwnColumns()
    {
        // Arrange
        var motion = SeedReadings.Motion(MeterLocation.Garage, From, motionDetected: true);
        var energy = SeedReadings.Energy(MeterLocation.Office, From.AddSeconds(1), energy: 845.62);
        var airQuality = SeedReadings.AirQuality(MeterLocation.LivingRoom, From.AddSeconds(2), co2: 803, pm25: 25, humidity: 36);

        await SeedAsync(motion, energy, airQuality);

        // Act
        using var response = await ExportAsync($"from={Format(From)}&to={Format(From.AddDays(1))}");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.ToString().ShouldBe("text/csv; charset=utf-8");

        var contentDisposition = response.Content.Headers.ContentDisposition.ShouldNotBeNull();
        contentDisposition.DispositionType.ShouldBe("attachment");
        contentDisposition.FileName.ShouldBe("meter-readings_20261101T120000Z_20261102T120000Z.csv");

        (await ReadLinesAsync(response)).ShouldBe(
        [
            Header,
            $"{motion.Id},Motion,Garage,2026-11-01T12:00:00.0000000Z,true,,,,",
            $"{energy.Id},Energy,Office,2026-11-01T12:00:01.0000000Z,,845.62,,,",
            $"{airQuality.Id},AirQuality,LivingRoom,2026-11-01T12:00:02.0000000Z,,,803,25,36"
        ]);
    }

    [Fact]
    public async Task Export_ReadingsWithSharedReceiveTimes_ReturnsOldestFirstWithIdBreakingTies()
    {
        // Arrange
        // A poll stores all meters with the same receive time; the id orders them.
        await SeedAsync(
            SeedReadings.Energy(MeterLocation.Kitchen, From.AddSeconds(15), id: Id(1)),
            SeedReadings.Motion(MeterLocation.Garage, From, id: Id(3)),
            SeedReadings.AirQuality(MeterLocation.Office, From, id: Id(2)),
            SeedReadings.Energy(MeterLocation.Kitchen, From, id: Id(4)));

        // Act
        using var response = await ExportAsync($"from={Format(From)}&to={Format(From.AddHours(1))}");

        // Assert
        var ids = (await ReadLinesAsync(response)).Skip(1).Select(line => Guid.Parse(line.Split(',')[0]));
        ids.ShouldBe([Id(2), Id(3), Id(4), Id(1)]);
    }

    [Fact]
    public async Task Export_FilterByTypesLocationsAndRange_ReturnsMatchingRowsOnly()
    {
        // Arrange
        var to = From.AddMinutes(10);

        var atFrom = SeedReadings.Energy(MeterLocation.Kitchen, From);
        var airQuality = SeedReadings.AirQuality(MeterLocation.Office, From.AddMinutes(5));
        var beforeTo = SeedReadings.Energy(MeterLocation.Office, to.AddSeconds(-1));

        await SeedAsync(
            atFrom,
            airQuality,
            beforeTo,
            SeedReadings.Energy(MeterLocation.Kitchen, From.AddSeconds(-1)),
            SeedReadings.Energy(MeterLocation.Kitchen, to),
            SeedReadings.Energy(MeterLocation.Garage, From.AddMinutes(1)),
            SeedReadings.Motion(MeterLocation.Kitchen, From.AddMinutes(1)));

        // Act
        // Enum names bind ignoring case; a parameter is repeated for several values.
        using var response = await ExportAsync(
            $"from={Format(From)}&to={Format(to)}&types=Energy&types=airquality&locations=Kitchen&locations=OFFICE");

        // Assert
        var ids = (await ReadLinesAsync(response)).Skip(1).Select(line => Guid.Parse(line.Split(',')[0]));
        ids.ShouldBe([atFrom.Id, airQuality.Id, beforeTo.Id]);
    }

    [Fact]
    public async Task Export_RangeWithOffset_FiltersByInstantAndNamesFileInUtc()
    {
        // Arrange
        var inRange = SeedReadings.Energy(MeterLocation.Kitchen, From);
        await SeedAsync(inRange, SeedReadings.Energy(MeterLocation.Kitchen, From.AddHours(-1)));

        var offset = TimeSpan.FromHours(2);

        // Act
        // 14:00+02:00 is 12:00 UTC; the '+' must be escaped in the query string.
        using var response = await ExportAsync($"from={Format(From.ToOffset(offset))}&to={Format(From.AddHours(1).ToOffset(offset))}");

        // Assert
        response.Content.Headers.ContentDisposition?.FileName.ShouldBe("meter-readings_20261101T120000Z_20261101T130000Z.csv");

        var lines = await ReadLinesAsync(response);
        lines.Length.ShouldBe(2);
        lines[1].ShouldStartWith(inRange.Id.ToString());
    }

    [Fact]
    public async Task Export_NoReadingsInRange_ReturnsHeaderOnly()
    {
        // Arrange
        await SeedAsync(SeedReadings.Energy(MeterLocation.Kitchen, From.AddDays(-1)));

        // Act
        using var response = await ExportAsync($"from={Format(From)}&to={Format(From.AddDays(7))}");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await ReadLinesAsync(response)).ShouldBe([Header]);
    }

    [Theory]
    [InlineData("to=2026-11-02T12:00:00Z", "'From' parameter")]
    [InlineData("from=2026-11-01T12:00:00Z", "'To' parameter")]
    [InlineData("from=yesterday&to=2026-11-02T12:00:00Z", "'yesterday' is not valid")]
    [InlineData("from=2026-11-01T12:00:00Z&to=2026-11-01T12:00:00Z", "'From' must be earlier than 'To'.")]
    [InlineData("from=2026-11-01T12:00:00Z&to=2026-11-08T12:00:01Z", "'To' must be at most 7 days after 'From'.")]
    [InlineData("from=2026-11-01T12:00:00Z&to=2026-11-02T12:00:00Z&types=Water", "'Water' is not valid")]
    [InlineData("from=2026-11-01T12:00:00Z&to=2026-11-02T12:00:00Z&locations=7", "'7' is invalid")]
    public async Task Export_InvalidQuery_ReturnsBadRequestProblem(string query, string expectedMessage)
    {
        // Act
        using var response = await ExportAsync(query);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
        response.Content.Headers.ContentDisposition.ShouldBeNull();

        var problem = await ReadJsonAsync(response);
        problem.GetProperty("status").GetInt32().ShouldBe((int)HttpStatusCode.BadRequest);
        ProblemMessages(problem).ShouldContain(message => message.Contains(expectedMessage, StringComparison.Ordinal));
    }

    [Fact]
    public async Task Export_DatabaseUnreachable_ReturnsInternalErrorProblemWithoutConnectionDetails()
    {
        // Arrange
        var port = TcpPorts.GetUnused().ToString(CultureInfo.InvariantCulture);
        var client = CreateClient(new Dictionary<string, string?>
        {
            ["Database:ConnectionString"] = $"Host=127.0.0.1;Port={port};Database=pulse_analytics;Username=gateway;Password=secret"
        });

        // Act
        using var response = await client.GetAsync(
            new Uri($"/api/readings/export?from={Format(From)}&to={Format(From.AddDays(1))}", UriKind.Relative),
            Ct);

        // Assert
        // The first row is read before anything is written, so the failure still gets an error response.
        response.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
        response.Content.Headers.ContentDisposition.ShouldBeNull();

        var content = await response.Content.ReadAsStringAsync(Ct);
        var problem = JsonDocument.Parse(content).RootElement;
        problem.GetProperty("status").GetInt32().ShouldBe((int)HttpStatusCode.InternalServerError);
        problem.GetProperty("detail").GetString().ShouldBe("An unexpected error occurred.");
        problem.GetProperty("traceId").GetString().ShouldNotBeNullOrWhiteSpace();

        content.ShouldNotContain("127.0.0.1");
        content.ShouldNotContain(port);
        content.ShouldNotContain("secret");
        content.ShouldNotContain("Npgsql", Case.Insensitive);
    }

    [Fact]
    public async Task Export_SeededRange_CountsCompletedExportAndItsRows()
    {
        // Arrange
        await SeedAsync(
            SeedReadings.Energy(MeterLocation.Kitchen, From),
            SeedReadings.Motion(MeterLocation.Garage, From.AddSeconds(1)),
            SeedReadings.AirQuality(MeterLocation.Office, From.AddSeconds(2)),
            SeedReadings.Energy(MeterLocation.Kitchen, From.AddHours(2)));

        var factory = CreateFactory();
        var client = factory.CreateClient();
        var meterFactory = factory.Services.GetRequiredService<IMeterFactory>();

        // The Grafana dashboard queries these names, so they are spelled out here instead of taken from the code.
        using var exports = new MetricCollector<long>(meterFactory, MeterName, "api_gateway.exports");
        using var rows = new MetricCollector<long>(meterFactory, MeterName, "api_gateway.export.rows");

        // Act
        using var response = await ExportAsync($"from={Format(From)}&to={Format(From.AddHours(1))}", client);
        await response.Content.ReadAsStringAsync(Ct);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        // The export records its metrics after its last flush, so they can arrive just after the response body.
        (await ObserveUntilAsync(exports, totals => totals.GetValueOrDefault("completed") == 1))
            .ShouldBe(new Dictionary<string, long> { ["completed"] = 1, ["cancelled"] = 0, ["failed"] = 0 });

        (await ObserveUntilAsync(rows, totals => totals.GetValueOrDefault("completed") == 3))
            .ShouldBe(new Dictionary<string, long> { ["completed"] = 3, ["cancelled"] = 0, ["failed"] = 0 });
    }

    [Fact]
    public void Startup_NoExportYet_ReportsEveryExportOutcomeAtZero()
    {
        // Arrange
        var factory = CreateFactory();
        factory.CreateClient().Dispose();

        var meterFactory = factory.Services.GetRequiredService<IMeterFactory>();
        using var exports = new MetricCollector<long>(meterFactory, MeterName, "api_gateway.exports");
        using var rows = new MetricCollector<long>(meterFactory, MeterName, "api_gateway.export.rows");

        // Act
        var exportTotals = Observe(exports);
        var rowTotals = Observe(rows);

        // Assert
        // The instruments are created at startup, so every outcome's series exists before its first export,
        // which lets Prometheus' rate() and increase() count that export.
        var zeroPerOutcome = new Dictionary<string, long> { ["completed"] = 0, ["cancelled"] = 0, ["failed"] = 0 };
        exportTotals.ShouldBe(zeroPerOutcome);
        rowTotals.ShouldBe(zeroPerOutcome);
    }

    // Reads the gateway's observable counters once: the current total per export outcome.
    private static Dictionary<string, long> Observe(MetricCollector<long> collector)
    {
        collector.Clear();
        collector.RecordObservableInstruments();

        return collector
            .GetMeasurementSnapshot()
            .ToDictionary(measurement => (string)measurement.Tags["app.outcome"]!, measurement => measurement.Value);
    }

    private static async Task<Dictionary<string, long>> ObserveUntilAsync(
        MetricCollector<long> collector,
        Func<Dictionary<string, long>, bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        var totals = Observe(collector);

        while (!condition(totals) && DateTime.UtcNow < deadline)
        {
            await Task.Delay(50, Ct);
            totals = Observe(collector);
        }

        return totals;
    }

    private static Guid Id(int value)
    {
        return Guid.Parse($"00000000-0000-0000-0000-{value:D12}");
    }

    private static string Format(DateTimeOffset timestamp)
    {
        return Uri.EscapeDataString(timestamp.ToString("O", CultureInfo.InvariantCulture));
    }

    private static async Task<string[]> ReadLinesAsync(HttpResponseMessage response)
    {
        var content = await response.Content.ReadAsStringAsync(Ct);

        content.ShouldEndWith("\r\n");

        return content[..^2].Split("\r\n");
    }

    private static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response)
    {
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct)).RootElement.Clone();
    }

    // The service's validation message is the detail; model binding errors are listed per parameter.
    private static IEnumerable<string> ProblemMessages(JsonElement problem)
    {
        if (problem.TryGetProperty("detail", out var detail) && detail.GetString() is { } detailMessage)
        {
            yield return detailMessage;
        }

        if (problem.TryGetProperty("errors", out var errors))
        {
            foreach (var parameter in errors.EnumerateObject())
            {
                foreach (var message in parameter.Value.EnumerateArray())
                {
                    yield return message.GetString()!;
                }
            }
        }
    }

    private async Task<HttpResponseMessage> ExportAsync(string query, HttpClient? client = null)
    {
        return await (client ?? CreateClient()).GetAsync(new Uri($"/api/readings/export?{query}", UriKind.Relative), Ct);
    }

    private HttpClient CreateClient(IReadOnlyDictionary<string, string?>? overrides = null)
    {
        return CreateFactory(overrides).CreateClient();
    }

    private ApiGatewayApplicationFactory CreateFactory(IReadOnlyDictionary<string, string?>? overrides = null)
    {
        var factory = new ApiGatewayApplicationFactory(postgreSql, overrides);
        _factories.Add(factory);

        return factory;
    }

    private async Task SeedAsync(params SeedReading[] readings)
    {
        await MeterReadingsTable.InsertAsync(postgreSql, readings, Ct);
    }
}
