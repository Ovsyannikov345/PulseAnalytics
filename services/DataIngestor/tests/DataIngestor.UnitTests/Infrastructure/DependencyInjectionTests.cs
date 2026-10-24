using DataIngestor.Application.Interfaces.Clients;
using DataIngestor.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace DataIngestor.UnitTests.Infrastructure;

public sealed class DependencyInjectionTests
{
    private const string ValidBaseUrl = "http://weakapp.test";
    private const string ValidApiKey = "test-api-key";

    [Fact]
    public void AddInfrastructureServices_ValidWeakAppSettings_ResolvesExternalMetersClient()
    {
        // Arrange
        using var serviceProvider = BuildServiceProvider(ValidBaseUrl, ValidApiKey);

        // Act
        var client = serviceProvider.GetService<IExternalMetersClient>();

        // Assert
        client.ShouldNotBeNull();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-a-url")]
    [InlineData("weakapp.test")]
    public void AddInfrastructureServices_InvalidBaseUrl_FailsValidationWhenClientIsResolved(string? baseUrl)
    {
        // Arrange
        using var serviceProvider = BuildServiceProvider(baseUrl, ValidApiKey);

        // Act
        var act = () => serviceProvider.GetRequiredService<IExternalMetersClient>();

        // Assert
        var exception = act.ShouldThrow<OptionsValidationException>();
        exception.Failures.ShouldContain(failure => failure.Contains("BaseUrl", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void AddInfrastructureServices_MissingApiKey_FailsValidationWhenClientIsResolved(string? apiKey)
    {
        // Arrange
        using var serviceProvider = BuildServiceProvider(ValidBaseUrl, apiKey);

        // Act
        var act = () => serviceProvider.GetRequiredService<IExternalMetersClient>();

        // Assert
        var exception = act.ShouldThrow<OptionsValidationException>();
        exception.Failures.ShouldContain(failure => failure.Contains("ApiKey", StringComparison.Ordinal));
    }

    private static ServiceProvider BuildServiceProvider(string? baseUrl, string? apiKey)
    {
        var settings = new Dictionary<string, string?>();

        if (baseUrl is not null)
        {
            settings["WeakApp:BaseUrl"] = baseUrl;
        }

        if (apiKey is not null)
        {
            settings["WeakApp:ApiKey"] = apiKey;
        }

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(settings)
            .Build();

        return new ServiceCollection()
            .AddLogging()
            .AddInfrastructureServices(configuration)
            .BuildServiceProvider();
    }
}
