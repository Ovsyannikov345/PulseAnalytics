using DataProcessor.Application.MeterReadings.Models;
using DataProcessor.Domain.Constants.Limits;
using FluentValidation;

namespace DataProcessor.Application.MeterReadings.Validators;

internal sealed class EnergyReadingModelValidator : MeterReadingModelValidator<EnergyReadingModel>
{
    public EnergyReadingModelValidator(TimeProvider timeProvider)
        : base(timeProvider)
    {
        RuleFor(m => m.Energy)
            .Cascade(CascadeMode.Stop)
            .Must(double.IsFinite)
            .WithMessage("'{PropertyName}' must be a finite number.")
            .GreaterThanOrEqualTo(EnergyLimits.MinEnergy);
    }
}
