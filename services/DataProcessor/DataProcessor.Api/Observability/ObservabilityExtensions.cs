using System.Reflection;
using DataProcessor.Api.Configuration;
using DataProcessor.Application.Telemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace DataProcessor.Api.Observability;

public static class ObservabilityExtensions
{
    private const int MetricExportIntervalInMilliseconds = 15_000;

    private const string RuntimeMeterName = "System.Runtime";

    private const string NpgsqlTelemetryName = "Npgsql";

    private const string RabbitMqSubscriberSourceName = "RabbitMQ.Client.Subscriber";

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
            .ConfigureResource(resource => resource.AddService(ProcessorTelemetry.ServiceName, serviceVersion: serviceVersion))
            .WithMetrics(metrics => metrics
                .AddMeter(ProcessorTelemetry.ScopeName)
                .AddMeter(RuntimeMeterName)
                .AddMeter(NpgsqlTelemetryName)
                .AddAspNetCoreInstrumentation()
                .AddOtlpExporter((exporterOptions, readerOptions) =>
                {
                    exporterOptions.Endpoint = otlpEndpoint;
                    readerOptions.PeriodicExportingMetricReaderOptions.ExportIntervalMilliseconds = MetricExportIntervalInMilliseconds;
                }))
            .WithTracing(tracing => tracing
                .AddSource(ProcessorTelemetry.ScopeName)
                .AddSource(RabbitMqSubscriberSourceName)
                .AddSource(NpgsqlTelemetryName)
                .AddAspNetCoreInstrumentation()
                .AddOtlpExporter(exporterOptions => exporterOptions.Endpoint = otlpEndpoint));

        return services;
    }
}
