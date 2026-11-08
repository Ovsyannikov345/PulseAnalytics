using System.Net;
using System.Text.Json;
using ApiGateway.IntegrationTests.Fixtures;
using Shouldly;
using Xunit;

namespace ApiGateway.IntegrationTests.Api.Rest;

/// <summary>
/// The OpenAPI document and Swagger UI, which are served in every environment (like Nitro on <c>/graphql</c>).
/// </summary>
public sealed class OpenApiDocumentTests(PostgreSqlContainerFixture postgreSql) : IAsyncDisposable
{
    private readonly ApiGatewayApplicationFactory _factory = new(postgreSql);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask DisposeAsync()
    {
        await _factory.DisposeAsync();
    }

    [Fact]
    public async Task OpenApiDocument_DescribesExportEndpoint()
    {
        // Arrange
        using var client = _factory.CreateClient();

        // Act
        using var response = await client.GetAsync(new Uri("/openapi/v1.json", UriKind.Relative), Ct);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct)).RootElement;
        document.GetProperty("info").GetProperty("title").GetString().ShouldBe("Pulse Analytics API Gateway");

        var operation = document.GetProperty("paths").GetProperty("/api/readings/export").GetProperty("get");
        operation.GetProperty("responses").GetProperty("200").GetProperty("content").TryGetProperty("text/csv; charset=utf-8", out _).ShouldBeTrue();

        var parameters = operation.GetProperty("parameters").EnumerateArray().ToDictionary(p => p.GetProperty("name").GetString()!);
        parameters.Keys.ShouldBe(["from", "to", "types", "locations"], ignoreOrder: true);
        parameters["from"].GetProperty("required").GetBoolean().ShouldBeTrue();
        parameters["to"].GetProperty("required").GetBoolean().ShouldBeTrue();
    }

    [Fact]
    public async Task OpenApiDocument_TakesDescriptionsFromXmlComments()
    {
        // Arrange
        using var client = _factory.CreateClient();

        // Act
        var content = await client.GetStringAsync(new Uri("/openapi/v1.json", UriKind.Relative), Ct);

        // Assert
        var operation = JsonDocument.Parse(content).RootElement
            .GetProperty("paths").GetProperty("/api/readings/export").GetProperty("get");

        operation.GetProperty("summary").GetString().ShouldBe("Exports readings as a CSV file.");
        operation.GetProperty("description").GetString().ShouldStartWith("Streams the readings received in [from, to)");
        operation.GetProperty("responses").GetProperty("400").GetProperty("description").GetString()
            .ShouldBe("A value is missing or invalid, e.g. the range is longer than 7 days.");

        var from = operation.GetProperty("parameters").EnumerateArray().Single(p => p.GetProperty("name").GetString() == "from");
        from.GetProperty("description").GetString().ShouldBe("Start of the range (inclusive), ISO 8601 with an offset, e.g. 2026-11-01T00:00:00Z.");
    }

    [Fact]
    public async Task OpenApiDocument_ListsEnumValuesByName()
    {
        // Arrange
        using var client = _factory.CreateClient();

        // Act
        var document = await client.GetStringAsync(new Uri("/openapi/v1.json", UriKind.Relative), Ct);

        // Assert
        // The endpoint binds names, so the document must not describe the enums as integers.
        document.ShouldContain("\"AirQuality\"");
        document.ShouldContain("\"LivingRoom\"");
    }

    [Fact]
    public async Task SwaggerUi_ReturnsPage()
    {
        // Arrange
        using var client = _factory.CreateClient();

        // Act
        using var response = await client.GetAsync(new Uri("/swagger/index.html", UriKind.Relative), Ct);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("text/html");
    }
}
