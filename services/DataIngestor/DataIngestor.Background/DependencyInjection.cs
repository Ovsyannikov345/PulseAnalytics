using DataIngestor.Background.Configuration;
using DataIngestor.Background.Workers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

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

        // Workers
        services.AddHostedService<MeterPollingWorker>();

        return services;
    }
}
