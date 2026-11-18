using NotificationService.Api.Configuration;
using NotificationService.Api.Hubs.Readings;
using NotificationService.Application.LiveReadings;
using Serilog;

namespace NotificationService.Api;

public static class DependencyInjection
{
    // Clients send only the handshake and pings.
    private const int MaximumReceiveMessageSizeInBytes = 4 * 1024;

    public static IServiceCollection AddReadingsHub(this IServiceCollection services)
    {
        services
            .AddSignalR(options => options.MaximumReceiveMessageSize = MaximumReceiveMessageSizeInBytes)
            .AddJsonProtocol(options => ReadingsHubJsonOptions.Configure(options.PayloadSerializerOptions));

        services.AddSingleton<IReadingsNotifier, SignalRReadingsNotifier>();

        return services;
    }

    public static IServiceCollection AddSerilogLogging(this IServiceCollection services, IConfiguration configuration)
    {
        var seqSettings = configuration.GetSection(SeqSettings.SectionName).Get<SeqSettings>();

        services.AddLogging(logging => logging.ClearProviders());

        services.AddSerilog(
            (serviceProvider, loggerConfiguration) =>
            {
                loggerConfiguration
                    .ReadFrom.Configuration(configuration)
                    .ReadFrom.Services(serviceProvider)
                    .Enrich.FromLogContext();

                if (!string.IsNullOrWhiteSpace(seqSettings?.ServerUrl))
                {
                    loggerConfiguration.WriteTo.Seq(seqSettings.ServerUrl, apiKey: seqSettings.ApiKey);
                }
            },
            writeToProviders: true);

        return services;
    }
}
