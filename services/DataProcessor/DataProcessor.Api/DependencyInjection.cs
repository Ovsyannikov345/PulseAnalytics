using DataProcessor.Api.Configuration;
using DataProcessor.Api.Logging;
using Serilog;

namespace DataProcessor.Api;

public static class DependencyInjection
{
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
                    .Enrich.FromLogContext()
                    .Enrich.With<CorrelationIdEnricher>();

                if (!string.IsNullOrWhiteSpace(seqSettings?.ServerUrl))
                {
                    loggerConfiguration.WriteTo.Seq(seqSettings.ServerUrl, apiKey: seqSettings.ApiKey);
                }
            },
            writeToProviders: true);

        return services;
    }
}
