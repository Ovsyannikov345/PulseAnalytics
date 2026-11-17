using DataProcessor.Application.Interfaces.Database;
using DataProcessor.Application.Interfaces.Messaging;
using DataProcessor.Infrastructure.Database;
using DataProcessor.Infrastructure.Database.Configuration;
using DataProcessor.Infrastructure.RabbitMq.Configuration;
using DataProcessor.Infrastructure.RabbitMq.Connection;
using DataProcessor.Infrastructure.RabbitMq.Constants;
using DataProcessor.Infrastructure.RabbitMq.Consumers;
using DataProcessor.Infrastructure.RabbitMq.Publishers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace DataProcessor.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services, IConfiguration configuration)
    {
        // Configuration
        services.AddOptions<RabbitMqSettings>()
            .Bind(configuration.GetSection(RabbitMqSettings.SectionName))
            .ValidateOnStart()
            .ValidateDataAnnotations();

        services.AddOptions<DatabaseSettings>()
            .Bind(configuration.GetSection(DatabaseSettings.SectionName))
            .ValidateOnStart()
            .ValidateDataAnnotations();

        // Database
        services.AddDbContext<IApplicationDbContext, ApplicationDbContext>((serviceProvider, options) =>
        {
            var databaseSettings = serviceProvider.GetRequiredService<IOptions<DatabaseSettings>>().Value;

            options
                .UseNpgsql(databaseSettings.ConnectionString)
                .UseSnakeCaseNamingConvention();
        });

        // Messaging
        services.AddKeyedSingleton<IRabbitMqConnectionProvider>(
            RabbitMqConnectionKeys.Consuming,
            (serviceProvider, _) => CreateConnectionProvider(serviceProvider, settings => settings.ClientProvidedName));

        services.AddKeyedSingleton<IRabbitMqConnectionProvider>(
            RabbitMqConnectionKeys.Publishing,
            (serviceProvider, _) => CreateConnectionProvider(serviceProvider, settings => $"{settings.ClientProvidedName}.publisher"));

        services.AddHostedService<MotionReadingsConsumer>();
        services.AddHostedService<EnergyReadingsConsumer>();
        services.AddHostedService<AirQualityReadingsConsumer>();

        services.AddSingleton<IStoredReadingsPublisher, StoredReadingsPublisher>();

        return services;
    }

    private static RabbitMqConnectionProvider CreateConnectionProvider(
        IServiceProvider serviceProvider,
        Func<RabbitMqSettings, string> clientProvidedName)
    {
        var rabbitMqSettings = serviceProvider.GetRequiredService<IOptions<RabbitMqSettings>>().Value;

        var connectionFactory = new ConnectionFactory
        {
            HostName = rabbitMqSettings.Host,
            Port = rabbitMqSettings.Port,
            VirtualHost = rabbitMqSettings.VirtualHost,
            UserName = rabbitMqSettings.UserName,
            Password = rabbitMqSettings.Password,
            ClientProvidedName = clientProvidedName(rabbitMqSettings),
            AutomaticRecoveryEnabled = true
        };

        return ActivatorUtilities.CreateInstance<RabbitMqConnectionProvider>(serviceProvider, connectionFactory);
    }
}
