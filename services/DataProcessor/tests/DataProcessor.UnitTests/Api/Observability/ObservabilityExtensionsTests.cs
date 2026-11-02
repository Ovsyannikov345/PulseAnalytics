using DataProcessor.Api.Observability;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using Shouldly;
using Xunit;

namespace DataProcessor.UnitTests.Api.Observability;

public sealed class ObservabilityExtensionsTests
{
    private const string OtlpEndpoint = "http://localhost:4317";

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void AddObservability_OtlpEndpointNotConfigured_RegistersNoTelemetryProviders(string? otlpEndpoint)
    {
        // Act
        using var serviceProvider = BuildServiceProvider(otlpEndpoint);

        // Assert
        serviceProvider.GetService<MeterProvider>().ShouldBeNull();
        serviceProvider.GetService<TracerProvider>().ShouldBeNull();
    }

    [Fact]
    public void AddObservability_OtlpEndpointConfigured_RegistersMeterAndTracerProviders()
    {
        // Act
        using var serviceProvider = BuildServiceProvider(OtlpEndpoint);

        // Assert
        serviceProvider.GetService<MeterProvider>().ShouldNotBeNull();
        serviceProvider.GetService<TracerProvider>().ShouldNotBeNull();
    }

    private static ServiceProvider BuildServiceProvider(string? otlpEndpoint)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["OpenTelemetry:OtlpEndpoint"] = otlpEndpoint })
            .Build();

        return new ServiceCollection()
            .AddObservability(configuration)
            .BuildServiceProvider();
    }
}
