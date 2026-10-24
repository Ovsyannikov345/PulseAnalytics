using DataIngestor.Background.Configuration;
using DataIngestor.Background.Workers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace DataIngestor.Background;

public static class DependencyInjection
{
    public static IServiceCollection AddBackgroundServices(this IServiceCollection services, IConfiguration configuration)
    {
        // Configuration
        services.AddOptions<MeterPollingWorkerSettings>()
            .Bind(configuration.GetSection(MeterPollingWorkerSettings.SectionName))
            .ValidateOnStart()
            .ValidateDataAnnotations();

        services.TryAddSingleton(TimeProvider.System);

        // Workers
        services.AddHostedService<MeterPollingWorker>();

        return services;
    }
}
