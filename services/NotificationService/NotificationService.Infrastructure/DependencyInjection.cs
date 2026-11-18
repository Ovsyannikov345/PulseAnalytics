using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NotificationService.Infrastructure.RabbitMq.Configuration;
using NotificationService.Infrastructure.RabbitMq.Connection;
using NotificationService.Infrastructure.RabbitMq.Consumers;
using NotificationService.Infrastructure.RabbitMq.Topology;
using RabbitMQ.Client;

namespace NotificationService.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services, IConfiguration configuration)
    {
        // Configuration
        services.AddOptions<RabbitMqSettings>()
            .Bind(configuration.GetSection(RabbitMqSettings.SectionName))
            .ValidateOnStart()
            .ValidateDataAnnotations();

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

        services.AddSingleton<ServiceInstance>();

        services.AddHostedService<MotionReadingsConsumer>();
        services.AddHostedService<EnergyReadingsConsumer>();
        services.AddHostedService<AirQualityReadingsConsumer>();

        return services;
    }
}
