using DataProcessor.Application.MeterReadings.Models;
using FluentValidation;

namespace DataProcessor.Application.MeterReadings.Validators;

internal abstract class MeterReadingModelValidator<TModel> : AbstractValidator<TModel>
    where TModel : MeterReadingModel
{
    private static readonly TimeSpan MaxClockSkew = TimeSpan.FromMinutes(1);

    protected MeterReadingModelValidator(TimeProvider timeProvider)
    {
        RuleFor(m => m.DeduplicationKey)
            .NotEmpty();

        RuleFor(m => m.Location)
            .IsInEnum();

        RuleFor(m => m.ReceivedAt)
            .NotEqual(default(DateTimeOffset))
            .Must(receivedAt => receivedAt <= timeProvider.GetUtcNow() + MaxClockSkew)
            .WithMessage("'{PropertyName}' must not be in the future.");
    }
}
