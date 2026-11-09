using System.Globalization;
using System.Net;
using System.Text.Json;
using ApiGateway.IntegrationTests.Fixtures;
using ApiGateway.IntegrationTests.Utilities;
using Shouldly;
using Xunit;

namespace ApiGateway.IntegrationTests.Api.GraphQL;

/// <summary>
/// How the gateway reports invalid input and database failures to GraphQL clients.
/// </summary>
public sealed class ErrorHandlingTests(PostgreSqlContainerFixture postgreSql) : IAsyncLifetime
{
    private const string ReadingsQuery = "{ readings { nodes { id } } }";

    private readonly List<ApiGatewayApplicationFactory> _factories = [];

    public async ValueTask InitializeAsync()
    {
        await MeterReadingsTable.ClearAsync(postgreSql, TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var factory in _factories)
        {
            await factory.DisposeAsync();
        }
    }

    [Theory]
    [InlineData("""{ readings(filter: { from: "2026-11-01T12:00:00Z", to: "2026-11-01T12:00:00Z" }) { nodes { id } } }""")]
    [InlineData("""{ readings(after: "not-a-cursor") { nodes { id } } }""")]
    [InlineData("""{ readings(first: 0) { nodes { id } } }""")]
    public async Task Readings_InvalidInput_ReturnsBadRequestError(string query)
    {
        // Arrange
        var client = CreateClient();

        // Act
        var response = await client.ExecuteAsync(query);

        // Assert
        // A field error keeps the 200 status; the error code is GraphQL's equivalent of 400 Bad Request.
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Data.GetProperty("readings").ValueKind.ShouldBe(JsonValueKind.Null);

        var error = response.Errors.ShouldHaveSingleItem();
        error.GetProperty("extensions").GetProperty("code").GetString().ShouldBe("BAD_REQUEST");
        error.GetProperty("message").GetString().ShouldNotBeNullOrWhiteSpace();
    }

    [Theory]
    [InlineData("""{ energyStatistics(filter: { from: "2026-11-01T00:00:00Z", to: "2026-12-02T00:00:01Z" }, groupBy: []) { samples } }""")]
    [InlineData("""{ energyStatistics(filter: { from: "2026-11-01T00:00:00Z", to: "2026-11-01T16:41:00Z" }, groupBy: [TIME], bucket: MINUTE) { samples } }""")]
    [InlineData("""{ airQualityStatistics(filter: { from: "2026-11-01T00:00:00Z", to: "2026-11-01T01:00:00Z" }, groupBy: [TIME]) { samples } }""")]
    [InlineData("""{ motionStatistics(filter: { from: "2026-11-01T00:00:00Z", to: "2026-11-01T01:00:00Z" }, groupBy: [LOCATION], bucket: HOUR) { samples } }""")]
    public async Task Statistics_InvalidInput_ReturnsBadRequestError(string query)
    {
        // Arrange
        var client = CreateClient();

        // Act
        var response = await client.ExecuteAsync(query);

        // Assert
        // The statistics fields are non-null, so the error nulls the whole data entry, but the status stays 200.
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Data.ValueKind.ShouldBe(JsonValueKind.Null);

        var error = response.Errors.ShouldHaveSingleItem();
        error.GetProperty("extensions").GetProperty("code").GetString().ShouldBe("BAD_REQUEST");
        error.GetProperty("message").GetString().ShouldNotBeNullOrWhiteSpace();
    }

    [Theory]
    [InlineData("""{"query":""}""", "Request must contain either a query or a document id.")]
    [InlineData("""{"query":""", "Invalid JSON document.")]
    [InlineData("", "Invalid JSON document.")]
    [InlineData("""{"query":5}""", "Invalid 'query' value. Expected string or null, but got Number.")]
    public async Task MalformedRequest_ReturnsBadRequestErrorWithParserMessage(string body, string expectedMessage)
    {
        // Arrange
        var client = CreateClient();

        // Act
        var response = await client.PostRawAsync(body);

        // Assert
        // The request never reaches execution, so the status is the request error's 400, and there is no data entry.
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        response.Body.TryGetProperty("data", out _).ShouldBeFalse();

        var error = response.Errors.ShouldHaveSingleItem();
        error.GetProperty("extensions").GetProperty("code").GetString().ShouldBe("BAD_REQUEST");
        error.GetProperty("message").GetString().ShouldBe(expectedMessage);

        response.Content.ShouldNotContain("Exception");
    }

    [Fact]
    public async Task Readings_DatabaseUnreachable_ReturnsInternalErrorWithoutConnectionDetails()
    {
        // Arrange
        var port = TcpPorts.GetUnused().ToString(CultureInfo.InvariantCulture);
        var client = CreateClient(new Dictionary<string, string?>
        {
            ["Database:ConnectionString"] = $"Host=127.0.0.1;Port={port};Database=pulse_analytics;Username=gateway;Password=secret"
        });

        // Act
        var response = await client.ExecuteAsync(ReadingsQuery);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Data.GetProperty("readings").ValueKind.ShouldBe(JsonValueKind.Null);

        var error = response.Errors.ShouldHaveSingleItem();
        error.GetProperty("extensions").GetProperty("code").GetString().ShouldBe("INTERNAL_ERROR");
        error.GetProperty("message").GetString().ShouldBe("An unexpected error occurred.");

        response.Content.ShouldNotContain("127.0.0.1");
        response.Content.ShouldNotContain(port);
        response.Content.ShouldNotContain("secret");
        response.Content.ShouldNotContain("Npgsql", Case.Insensitive);
    }

    private GraphQLClient CreateClient(IReadOnlyDictionary<string, string?>? overrides = null)
    {
        var factory = new ApiGatewayApplicationFactory(postgreSql, overrides);
        _factories.Add(factory);

        return new GraphQLClient(factory.CreateClient());
    }
}
