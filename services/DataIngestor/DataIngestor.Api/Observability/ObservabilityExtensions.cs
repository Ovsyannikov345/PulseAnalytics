using System.Diagnostics;
using System.Reflection;
using DataIngestor.Api.Configuration;
using DataIngestor.Application.Telemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace DataIngestor.Api.Observability;

public static class ObservabilityExtensions
{
    private const int MetricExportIntervalInMilliseconds = 15_000;

    private const string RuntimeMeterName = "System.Runtime";

    private const string PollyMeterName = "Polly";

    private const string RabbitMqPublisherSourceName = "RabbitMQ.Client.Publisher";

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

        // Logs are not exported over OTLP: Serilog already ships them to Seq.
        services
            .AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService(IngestorTelemetry.ServiceName, serviceVersion: serviceVersion))
            .WithMetrics(metrics => metrics
                .AddMeter(IngestorTelemetry.ScopeName)
                .AddMeter(RuntimeMeterName)
                .AddMeter(PollyMeterName)
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddOtlpExporter((exporterOptions, readerOptions) =>
                {
                    exporterOptions.Endpoint = otlpEndpoint;
                    readerOptions.PeriodicExportingMetricReaderOptions.ExportIntervalMilliseconds = MetricExportIntervalInMilliseconds;
                }))
            .WithTracing(tracing => tracing
                .AddSource(IngestorTelemetry.ScopeName)
                .AddSource(RabbitMqPublisherSourceName)
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation(options =>
                    options.FilterHttpRequestMessage = _ => IsPartOfTrace(Activity.Current))
                .AddOtlpExporter(exporterOptions => exporterOptions.Endpoint = otlpEndpoint));

        return services;
    }

    private static bool IsPartOfTrace(Activity? httpRequestActivity)
    {
        return httpRequestActivity?.ParentSpanId != default(ActivitySpanId);
    }
}
