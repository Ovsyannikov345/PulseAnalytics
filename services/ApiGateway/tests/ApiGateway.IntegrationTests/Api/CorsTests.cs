using System.Net;
using ApiGateway.IntegrationTests.Fixtures;
using ApiGateway.IntegrationTests.Utilities;
using Shouldly;
using Xunit;

namespace ApiGateway.IntegrationTests.Api;

/// <summary>
/// Browser access from the frontend's origin (<c>Cors:AllowedOrigins</c>).
/// </summary>
public sealed class CorsTests(PostgreSqlContainerFixture postgreSql) : IAsyncLifetime
{
    private const string AllowedOrigin = "http://localhost:5173";

    private const string OtherOrigin = "http://evil.example";

    private const string AllowOriginHeader = "Access-Control-Allow-Origin";

    private const string ExposeHeadersHeader = "Access-Control-Expose-Headers";

    private readonly ApiGatewayApplicationFactory _factory = new(
        postgreSql,
        new Dictionary<string, string?> { ["Cors:AllowedOrigins:0"] = AllowedOrigin });

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        await MeterReadingsTable.ClearAsync(postgreSql, Ct);
    }

    public async ValueTask DisposeAsync()
    {
        await _factory.DisposeAsync();
    }

    [Fact]
    public async Task GraphQLPreflight_AllowedOrigin_AllowsPostWithContentType()
    {
        // Arrange
        using var client = _factory.CreateClient();
        using var request = Preflight(AllowedOrigin);

        // Act
        using var response = await client.SendAsync(request, Ct);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        response.Headers.GetValues(AllowOriginHeader).ShouldBe([AllowedOrigin]);
        response.Headers.GetValues("Access-Control-Allow-Methods").ShouldContain(method => method.Contains("POST"));
        response.Headers.GetValues("Access-Control-Allow-Headers").ShouldContain(header => header.Contains("content-type"));
        response.Headers.GetValues("Access-Control-Max-Age").ShouldBe(["600"]);
    }

    [Fact]
    public async Task GraphQLPreflight_OtherOrigin_AllowsNothing()
    {
        // Arrange
        using var client = _factory.CreateClient();
        using var request = Preflight(OtherOrigin);

        // Act
        using var response = await client.SendAsync(request, Ct);

        // Assert
        response.Headers.Contains(AllowOriginHeader).ShouldBeFalse();
    }

    [Fact]
    public async Task Export_AllowedOrigin_ExposesContentDisposition()
    {
        // Arrange
        using var client = _factory.CreateClient();
        using var request = ExportRequest(AllowedOrigin);


        // Act
        using var response = await client.SendAsync(request, Ct);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Headers.GetValues(AllowOriginHeader).ShouldBe([AllowedOrigin]);
        response.Headers.GetValues(ExposeHeadersHeader).ShouldContain(header => header.Contains("Content-Disposition"));
    }

    [Fact]
    public async Task Export_OtherOrigin_HasNoCorsHeaders()
    {
        // Arrange
        using var client = _factory.CreateClient();
        using var request = ExportRequest(OtherOrigin);


        // Act
        using var response = await client.SendAsync(request, Ct);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Headers.Contains(AllowOriginHeader).ShouldBeFalse();
        response.Headers.Contains(ExposeHeadersHeader).ShouldBeFalse();
    }

    private static HttpRequestMessage ExportRequest(string origin)
    {
        var request = new HttpRequestMessage(
            HttpMethod.Get,
            new Uri("/api/readings/export?from=2026-11-01T00:00:00Z&to=2026-11-02T00:00:00Z", UriKind.Relative));
        request.Headers.Add("Origin", origin);

        return request;
    }

    private static HttpRequestMessage Preflight(string origin)
    {
        var request = new HttpRequestMessage(HttpMethod.Options, new Uri("/graphql", UriKind.Relative));
        request.Headers.Add("Origin", origin);
        request.Headers.Add("Access-Control-Request-Method", "POST");
        request.Headers.Add("Access-Control-Request-Headers", "content-type");

        return request;
    }
}
