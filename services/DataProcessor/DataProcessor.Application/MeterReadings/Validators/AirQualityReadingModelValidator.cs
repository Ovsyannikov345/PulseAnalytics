using DataProcessor.Application.MeterReadings.Models;
using DataProcessor.Domain.Constants.Limits;
using FluentValidation;

namespace DataProcessor.Application.MeterReadings.Validators;

internal sealed class AirQualityReadingModelValidator : MeterReadingModelValidator<AirQualityReadingModel>
{
    public AirQualityReadingModelValidator(TimeProvider timeProvider)
        : base(timeProvider)
    {
        RuleFor(m => m.Co2)
            .InclusiveBetween(AirQualityLimits.MinCo2, AirQualityLimits.MaxCo2);

        RuleFor(m => m.Pm25)
            .InclusiveBetween(AirQualityLimits.MinPm25, AirQualityLimits.MaxPm25);

        RuleFor(m => m.Humidity)
            .InclusiveBetween(AirQualityLimits.MinHumidity, AirQualityLimits.MaxHumidity);
    }
}
