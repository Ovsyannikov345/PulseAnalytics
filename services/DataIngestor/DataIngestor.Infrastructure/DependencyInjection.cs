using DataIngestor.Application.Interfaces.Clients;
using DataIngestor.Application.Interfaces.Messaging;
using DataIngestor.Infrastructure.RabbitMq.Configuration;
using DataIngestor.Infrastructure.RabbitMq.Connection;
using DataIngestor.Infrastructure.RabbitMq.Publishers;
using DataIngestor.Infrastructure.WeakApp.Clients;
using DataIngestor.Infrastructure.WeakApp.Configuration;
using DataIngestor.Infrastructure.WeakApp.Constants;
using DataIngestor.Infrastructure.WeakApp.Resilience;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

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

        services.AddOptions<WeakAppResilienceSettings>()
            .Bind(configuration.GetSection(WeakAppResilienceSettings.SectionName))
            .ValidateOnStart()
            .ValidateDataAnnotations();

        services.AddOptions<RabbitMqSettings>()
            .Bind(configuration.GetSection(RabbitMqSettings.SectionName))
            .ValidateOnStart()
            .ValidateDataAnnotations();

        services.TryAddSingleton(TimeProvider.System);

        // HTTP Clients
        services.AddHttpClient<IExternalMetersClient, WeakAppClient>((serviceProvider, client) =>
        {
            var optionsMonitor = serviceProvider.GetRequiredService<IOptionsMonitor<WeakAppSettings>>();

            var weakAppSettings = optionsMonitor.CurrentValue;

            var baseUrl = $"{weakAppSettings.BaseUrl.TrimEnd('/')}/";

            client.BaseAddress = new Uri(baseUrl, UriKind.Absolute);
            client.DefaultRequestHeaders.Add(HeaderNames.ApiKey, weakAppSettings.ApiKey);
        })
        .AddResilienceHandler(
            WeakAppResiliencePipeline.Name,
            (builder, context) =>
            {
                var resilienceSettings = context.GetOptions<WeakAppResilienceSettings>();

                WeakAppResiliencePipeline.Configure(builder, resilienceSettings);
            });

        // Messaging
        services.AddSingleton<IConnectionFactory>(serviceProvider =>
        {
            var rabbitMqSettings = serviceProvider.GetRequiredService<IOptions<RabbitMqSettings>>().Value;

            return new ConnectionFactory
            {
                HostName = rabbitMqSettings.Host,
                Port = rabbitMqSettings.Port,
                VirtualHost = rabbitMqSettings.VirtualHost,
                UserName = rabbitMqSettings.UserName,
                Password = rabbitMqSettings.Password,
                ClientProvidedName = rabbitMqSettings.ClientProvidedName,
                AutomaticRecoveryEnabled = true
            };
        });

        services.AddSingleton<IRabbitMqConnectionProvider, RabbitMqConnectionProvider>();
        services.AddScoped<IMeterReadingsPublisher, MeterReadingsPublisher>();

        return services;
    }
}
