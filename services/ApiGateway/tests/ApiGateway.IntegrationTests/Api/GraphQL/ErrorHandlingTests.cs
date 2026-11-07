using System.Globalization;
using System.Net;
using System.Net.Sockets;
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

    [Fact]
    public async Task Readings_DatabaseUnreachable_ReturnsInternalErrorWithoutConnectionDetails()
    {
        // Arrange
        var port = GetUnusedPort().ToString(CultureInfo.InvariantCulture);
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

    /// <summary>
    /// A local port nothing listens on, so connecting to it is refused.
    /// </summary>
    private static int GetUnusedPort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);

        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();

        return port;
    }

    private GraphQLClient CreateClient(IReadOnlyDictionary<string, string?>? overrides = null)
    {
        var factory = new ApiGatewayApplicationFactory(postgreSql, overrides);
        _factories.Add(factory);

        return new GraphQLClient(factory.CreateClient());
    }
}
