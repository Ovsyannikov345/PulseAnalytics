using DataIngestor.Api.Configuration;
using DataIngestor.Api.Logging;
using Serilog;

namespace DataIngestor.Api;

public static class DependencyInjection
{
    public static IServiceCollection AddApiServices(this IServiceCollection services)
    {
        services.AddOpenApi();

        return services;
    }

    public static IServiceCollection AddSerilogLogging(this IServiceCollection services, IConfiguration configuration)
    {
        var seqSettings = configuration.GetSection(SeqSettings.SectionName).Get<SeqSettings>();

        services.AddSerilog((serviceProvider, loggerConfiguration) =>
        {
            loggerConfiguration
                .ReadFrom.Configuration(configuration)
                .ReadFrom.Services(serviceProvider)
                .Enrich.FromLogContext()
                .Enrich.With<CorrelationIdEnricher>();

            if (!string.IsNullOrWhiteSpace(seqSettings?.ServerUrl))
            {
                loggerConfiguration.WriteTo.Seq(seqSettings.ServerUrl, apiKey: seqSettings.ApiKey);
            }
        });

        return services;
    }
}
