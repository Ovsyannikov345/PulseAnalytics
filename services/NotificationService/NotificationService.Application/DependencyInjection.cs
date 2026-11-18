using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NotificationService.Application.LiveReadings;
using NotificationService.Application.Telemetry.Metrics;
using NotificationService.Application.Telemetry.Traces;

namespace NotificationService.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);

        services.AddSingleton<IMetricsService, MetricsService>();
        services.AddSingleton<ITracingService, TracingService>();

        services.AddSingleton<IReadingsBroadcastService, ReadingsBroadcastService>();

        return services;
    }
}
