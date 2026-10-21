using DataIngestor.Application.Interfaces.Clients;
using DataIngestor.Infrastructure.WeakApp.Clients;
using DataIngestor.Infrastructure.WeakApp.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace DataIngestor.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services, IConfiguration configuration)
    {
        // Configuration
        services.AddOptions<WeakAppSettings>()
            .Bind(configuration.GetSection(WeakAppSettings.SectionName))
            .ValidateOnStart()
            .ValidateDataAnnotations();

        // HTTP Clients
        services.AddHttpClient<IMetersClient, MetersClient>((serviceProvider, client) =>
        {
            var weakAppSettings = serviceProvider.GetRequiredService<IOptionsMonitor<WeakAppSettings>>();

            client.BaseAddress = new Uri(weakAppSettings.CurrentValue.BaseUrl, UriKind.Absolute);
        });

        return services;
    }
}
