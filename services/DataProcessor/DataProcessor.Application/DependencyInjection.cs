using DataProcessor.Application.MeterReadings;
using DataProcessor.Application.MeterReadings.Strategies;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace DataProcessor.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);

        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly, includeInternalTypes: true);

        services
            .AddScoped<IReadingProcessingStrategy, MotionReadingProcessingStrategy>()
            .AddScoped<IReadingProcessingStrategy, EnergyReadingProcessingStrategy>()
            .AddScoped<IReadingProcessingStrategy, AirQualityReadingProcessingStrategy>();

        services.AddScoped<IReadingsProcessingService, ReadingsProcessingService>();

        return services;
    }
}
