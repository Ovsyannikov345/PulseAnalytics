using System.Reflection;
using NotificationService.Api.Configuration;
using NotificationService.Api.HealthCheck;
using NotificationService.Application.Telemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace NotificationService.Api.Observability;

public static class ObservabilityExtensions
{
    private const int MetricExportIntervalInMilliseconds = 15_000;

    private const string RuntimeMeterName = "System.Runtime";

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
            .ConfigureResource(resource => resource.AddService(NotificationServiceTelemetry.ServiceName, serviceVersion: serviceVersion))
            .WithMetrics(metrics => metrics
                .AddMeter(NotificationServiceTelemetry.ScopeName)
                .AddMeter(RuntimeMeterName)
                .AddAspNetCoreInstrumentation()
                .AddOtlpExporter((exporterOptions, readerOptions) =>
                {
                    exporterOptions.Endpoint = otlpEndpoint;
                    readerOptions.PeriodicExportingMetricReaderOptions.ExportIntervalMilliseconds = MetricExportIntervalInMilliseconds;
                }))
            .WithTracing(tracing => tracing
                .AddSource(NotificationServiceTelemetry.ScopeName)
                .AddAspNetCoreInstrumentation(options => options.Filter = context => !context.IsHealthCheckRequest())
                .AddOtlpExporter(exporterOptions => exporterOptions.Endpoint = otlpEndpoint));

        return services;
    }
}
