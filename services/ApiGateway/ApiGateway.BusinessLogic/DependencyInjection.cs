using ApiGateway.BusinessLogic.MeterReadings;
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

        services.AddScoped<IMeterReadingsService, MeterReadingsService>();

        services.AddScoped<IReadingStatisticsService, ReadingStatisticsService>();

        services.AddScoped<IReadingsExportService, ReadingsExportService>();

        return services;
    }
}
