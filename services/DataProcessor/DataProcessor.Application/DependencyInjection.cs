using DataProcessor.Application.MeterReadings;
using DataProcessor.Application.MeterReadings.Models;
using DataProcessor.Application.MeterReadings.Strategies;
using DataProcessor.Application.MeterReadings.Validators;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace DataProcessor.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);

        services
            .AddSingleton<IValidator<MotionReadingModel>, MotionReadingModelValidator>()
            .AddSingleton<IValidator<EnergyReadingModel>, EnergyReadingModelValidator>()
            .AddSingleton<IValidator<AirQualityReadingModel>, AirQualityReadingModelValidator>();

        services
            .AddScoped<IReadingProcessingStrategy, MotionReadingProcessingStrategy>()
            .AddScoped<IReadingProcessingStrategy, EnergyReadingProcessingStrategy>()
            .AddScoped<IReadingProcessingStrategy, AirQualityReadingProcessingStrategy>();

        services.AddScoped<IReadingsProcessingService, ReadingsProcessingService>();

        return services;
    }
}
