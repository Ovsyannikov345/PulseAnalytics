using DataProcessor.Application.Readings;
using DataProcessor.Application.Readings.Strategies;
using Microsoft.Extensions.DependencyInjection;

namespace DataProcessor.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddSingleton<IReadingProcessingStrategy, MotionReadingProcessingStrategy>();
        services.AddSingleton<IReadingProcessingStrategy, EnergyReadingProcessingStrategy>();
        services.AddSingleton<IReadingProcessingStrategy, AirQualityReadingProcessingStrategy>();

        services.AddScoped<IReadingsProcessingService, ReadingsProcessingService>();

        return services;
    }
}
