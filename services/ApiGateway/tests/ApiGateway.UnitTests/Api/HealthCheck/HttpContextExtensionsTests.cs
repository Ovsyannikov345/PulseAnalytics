using ApiGateway.Api.HealthCheck;
using Microsoft.AspNetCore.Http;
using Shouldly;
using Xunit;

namespace ApiGateway.UnitTests.Api.HealthCheck;

public sealed class HttpContextExtensionsTests
{
    [Theory]
    [InlineData("/health")]
    [InlineData("/health/")]
    [InlineData("/HEALTH")]
    public void IsHealthCheckRequest_HealthEndpointPath_ReturnsTrue(string path)
    {
        // Arrange
        var context = CreateContext(path);

        // Act
        var result = context.IsHealthCheckRequest();

        // Assert
        result.ShouldBeTrue();
    }

    [Theory]
    [InlineData("/")]
    [InlineData("/healthz")]
    [InlineData("/graphql")]
    [InlineData("/api/health")]
    public void IsHealthCheckRequest_OtherPath_ReturnsFalse(string path)
    {
        // Arrange
        var context = CreateContext(path);

        // Act
        var result = context.IsHealthCheckRequest();

        // Assert
        result.ShouldBeFalse();
    }

    private static DefaultHttpContext CreateContext(string path)
    {
        return new DefaultHttpContext { Request = { Path = path } };
    }
}
