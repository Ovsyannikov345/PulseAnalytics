using DataProcessor.Application.MeterReadings;
using DataProcessor.Application.MeterReadings.Strategies;
using Microsoft.Extensions.DependencyInjection;

namespace DataProcessor.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services
            .AddScoped<IReadingProcessingStrategy, MotionReadingProcessingStrategy>()
            .AddScoped<IReadingProcessingStrategy, EnergyReadingProcessingStrategy>()
            .AddScoped<IReadingProcessingStrategy, AirQualityReadingProcessingStrategy>();

        services.AddScoped<IReadingsProcessingService, ReadingsProcessingService>();

        return services;
    }
}
