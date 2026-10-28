using DataIngestor.Application.ExternalMeters;
using DataIngestor.Application.Telemetry.Metrics;
using DataIngestor.Application.Telemetry.Traces;
using Microsoft.Extensions.DependencyInjection;

namespace DataIngestor.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddSingleton<IMetricsService, MetricsService>();
        services.AddSingleton<ITracingService, TracingService>();

        services.AddScoped<IExternalMetersService, ExternalMetersService>();

        return services;
    }
}
