using DataProcessor.Api.Observability;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OpenTelemetry.Instrumentation.AspNetCore;
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

    [Theory]
    [InlineData("/health", false)]
    [InlineData("/metrics", true)]
    public void AddObservability_OtlpEndpointConfigured_TracesAllRequestsExceptHealthChecks(string path, bool isTraced)
    {
        // Arrange
        using var serviceProvider = BuildServiceProvider(OtlpEndpoint);
        var filter = serviceProvider
            .GetRequiredService<IOptionsMonitor<AspNetCoreTraceInstrumentationOptions>>()
            .Get(Options.DefaultName)
            .Filter
            .ShouldNotBeNull();

        // Act
        var result = filter(new DefaultHttpContext { Request = { Path = path } });

        // Assert
        result.ShouldBe(isTraced);
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
