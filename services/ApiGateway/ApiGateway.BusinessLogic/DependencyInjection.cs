using ApiGateway.BusinessLogic.MeterReadings;
using ApiGateway.BusinessLogic.Telemetry.Metrics;
using ApiGateway.DataAccess;
using FluentValidation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ApiGateway.BusinessLogic;

public static class DependencyInjection
{
    public static IServiceCollection AddBusinessLogic(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDataAccess(configuration);

        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly, includeInternalTypes: true);

        services.AddSingleton<IMetricsService, MetricsService>();

        services
            .AddScoped<IMeterReadingsService, MeterReadingsService>()
            .AddScoped<IReadingStatisticsService, ReadingStatisticsService>()
            .AddScoped<IReadingsExportService, ReadingsExportService>();

        return services;
    }
}
