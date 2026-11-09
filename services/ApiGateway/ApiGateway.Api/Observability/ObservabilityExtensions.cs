using System.Reflection;
using ApiGateway.Api.Configuration;
using ApiGateway.Api.HealthCheck;
using ApiGateway.BusinessLogic.Telemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace ApiGateway.Api.Observability;

public static class ObservabilityExtensions
{
    private const int MetricExportIntervalInMilliseconds = 15_000;

    private const string RuntimeMeterName = "System.Runtime";

    private const string NpgsqlTelemetryName = "Npgsql";

    public static IServiceCollection AddObservability(this IServiceCollection services, IConfiguration configuration)
    {
        var openTelemetrySettings = configuration.GetSection(OpenTelemetrySettings.SectionName).Get<OpenTelemetrySettings>();

        if (string.IsNullOrWhiteSpace(openTelemetrySettings?.OtlpEndpoint))
        {
            return services;
        }

        var otlpEndpoint = new Uri(openTelemetrySettings.OtlpEndpoint, UriKind.Absolute);

        var serviceVersion = typeof(ObservabilityExtensions).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;

        services
            .AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService(ApiGatewayTelemetry.ServiceName, serviceVersion: serviceVersion))
            .WithMetrics(metrics => metrics
                .AddMeter(ApiGatewayTelemetry.ScopeName)
                .AddMeter(RuntimeMeterName)
                .AddMeter(NpgsqlTelemetryName)
                .AddAspNetCoreInstrumentation()
                .AddOtlpExporter((exporterOptions, readerOptions) =>
                {
                    exporterOptions.Endpoint = otlpEndpoint;
                    readerOptions.PeriodicExportingMetricReaderOptions.ExportIntervalMilliseconds = MetricExportIntervalInMilliseconds;
                }))
            .WithTracing(tracing => tracing
                .AddSource(NpgsqlTelemetryName)
                .AddHotChocolateInstrumentation()
                .AddAspNetCoreInstrumentation(options => options.Filter = context => !context.IsHealthCheckRequest())
                .AddOtlpExporter(exporterOptions => exporterOptions.Endpoint = otlpEndpoint));

        return services;
    }
}
