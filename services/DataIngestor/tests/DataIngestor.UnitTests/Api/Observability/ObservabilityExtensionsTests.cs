using System.Diagnostics;
using DataIngestor.Api.Observability;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OpenTelemetry.Instrumentation.Http;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using Shouldly;
using Xunit;

namespace DataIngestor.UnitTests.Api.Observability;

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

    [Fact]
    public void AddObservability_HttpRequestWithinTrace_RecordsRequestSpan()
    {
        // Arrange
        using var serviceProvider = BuildServiceProvider(OtlpEndpoint);
        var filter = GetHttpRequestFilter(serviceProvider);

        using var parentActivity = new Activity("poll meters").Start();
        using var httpRequestActivity = new Activity("GET").Start();

        // Act
        var recorded = filter(new HttpRequestMessage());

        // Assert
        recorded.ShouldBeTrue();
    }

    [Fact]
    public void AddObservability_HttpRequestOutsideTrace_DropsRequestSpan()
    {
        // Arrange
        using var serviceProvider = BuildServiceProvider(OtlpEndpoint);
        var filter = GetHttpRequestFilter(serviceProvider);

        using var httpRequestActivity = new Activity("GET").Start();

        // Act
        var recorded = filter(new HttpRequestMessage());

        // Assert
        recorded.ShouldBeFalse();
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

    private static Func<HttpRequestMessage, bool> GetHttpRequestFilter(IServiceProvider serviceProvider)
    {
        return serviceProvider.GetRequiredService<IOptionsMonitor<HttpClientTraceInstrumentationOptions>>()
            .CurrentValue.FilterHttpRequestMessage
            .ShouldNotBeNull();
    }
}
