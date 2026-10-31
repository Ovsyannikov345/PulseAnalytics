using System;
using System.Collections.Generic;
using System.Text;
using DataProcessor.Infrastructure.RabbitMq.Configuration;
using DataProcessor.Infrastructure.RabbitMq.Connection;
using DataProcessor.Infrastructure.RabbitMq.Consumers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
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

        services.AddHostedService<MeterReadingsConsumer>();

        return services;
    }
}
