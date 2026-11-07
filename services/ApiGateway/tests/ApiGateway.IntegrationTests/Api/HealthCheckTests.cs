using System.Net;
using ApiGateway.IntegrationTests.Fixtures;
using Shouldly;
using Xunit;

namespace ApiGateway.IntegrationTests.Api;

public sealed class HealthCheckTests(PostgreSqlContainerFixture postgreSql) : IAsyncDisposable
{
    private readonly ApiGatewayApplicationFactory _factory = new(postgreSql);

    [Fact]
    public async Task Health_HostStarted_ReturnsHealthy()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        using var client = _factory.CreateClient();

        // Act
        using var response = await client.GetAsync(new Uri("/health", UriKind.Relative), ct);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync(ct)).ShouldBe("Healthy");
    }

    public async ValueTask DisposeAsync()
    {
        await _factory.DisposeAsync();
    }
}
