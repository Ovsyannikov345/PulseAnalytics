using System.Diagnostics;
using ApiGateway.Api.Observability;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OpenTelemetry.Instrumentation.AspNetCore;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using Shouldly;
using Xunit;

namespace ApiGateway.UnitTests.Api.Observability;

public sealed class ObservabilityExtensionsTests
{
    // Nothing listens there, so disposing the providers can't push test telemetry into a running local collector.
    private const string OtlpEndpoint = "http://127.0.0.1:9";

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
    [InlineData("Npgsql", true)]
    [InlineData("HotChocolate.Diagnostics", true)]
    [InlineData("RabbitMQ.Client.Subscriber", false)]
    public void AddObservability_OtlpEndpointConfigured_TracesDatabaseAndGraphQLSources(string sourceName, bool isTraced)
    {
        // Arrange
        using var serviceProvider = BuildServiceProvider(OtlpEndpoint);
        serviceProvider.GetRequiredService<TracerProvider>();

        // Act
        using var source = new ActivitySource(sourceName);

        // Assert
        source.HasListeners().ShouldBe(isTraced);
    }

    [Theory]
    [InlineData("/health", false)]
    [InlineData("/graphql", true)]
    [InlineData("/api/readings/export", true)]
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
