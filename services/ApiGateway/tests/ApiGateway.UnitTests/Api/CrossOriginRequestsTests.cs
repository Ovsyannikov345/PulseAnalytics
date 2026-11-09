using ApiGateway.Api;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace ApiGateway.UnitTests.Api;

public sealed class CrossOriginRequestsTests
{
    [Fact]
    public void AddCrossOriginRequests_OriginsConfigured_DefaultPolicyAllowsThemForGetAndPost()
    {
        // Arrange
        using var serviceProvider = BuildServiceProvider("http://localhost:5173", "https://dashboard.example.com");

        // Act
        var policy = GetDefaultPolicy(serviceProvider);

        // Assert
        policy.Origins.ShouldBe(["http://localhost:5173", "https://dashboard.example.com"]);
        policy.Methods.ShouldBe(["GET", "POST"]);
        policy.AllowAnyHeader.ShouldBeTrue();
        policy.SupportsCredentials.ShouldBeFalse();
    }

    [Fact]
    public void AddCrossOriginRequests_OriginsConfigured_ExposesContentDispositionAndCachesPreflight()
    {
        // Arrange
        using var serviceProvider = BuildServiceProvider("http://localhost:5173");

        // Act
        var policy = GetDefaultPolicy(serviceProvider);

        // Assert
        policy.ExposedHeaders.ShouldBe(["Content-Disposition"]);
        policy.PreflightMaxAge.ShouldBe(TimeSpan.FromMinutes(10));
    }

    [Fact]
    public void AddCrossOriginRequests_NoOriginsConfigured_AllowsNoOrigin()
    {
        // Arrange
        using var serviceProvider = BuildServiceProvider();

        // Act
        var policy = GetDefaultPolicy(serviceProvider);

        // Assert
        policy.Origins.ShouldBeEmpty();
        policy.AllowAnyOrigin.ShouldBeFalse();
    }

    [Fact]
    public void AddCrossOriginRequests_OriginWithTrailingSlash_FailsOnStart()
    {
        // Arrange
        using var serviceProvider = BuildServiceProvider("http://localhost:5173/");
        var startupValidator = serviceProvider.GetRequiredService<IStartupValidator>();

        // Act
        var exception = Should.Throw<OptionsValidationException>(startupValidator.Validate);

        // Assert
        exception.Message.ShouldContain("'http://localhost:5173/' is not an origin.");
    }

    private static ServiceProvider BuildServiceProvider(params string[] allowedOrigins)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(allowedOrigins.Select((origin, index) =>
                new KeyValuePair<string, string?>($"Cors:AllowedOrigins:{index}", origin)))
            .Build();

        return new ServiceCollection()
            .AddCrossOriginRequests(configuration)
            .BuildServiceProvider();
    }

    private static CorsPolicy GetDefaultPolicy(ServiceProvider serviceProvider)
    {
        var corsOptions = serviceProvider.GetRequiredService<IOptions<CorsOptions>>().Value;

        return corsOptions.GetPolicy(corsOptions.DefaultPolicyName).ShouldNotBeNull();
    }
}
