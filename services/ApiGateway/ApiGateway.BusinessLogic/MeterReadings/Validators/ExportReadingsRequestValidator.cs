using ApiGateway.BusinessLogic.MeterReadings.Constants;
using ApiGateway.BusinessLogic.MeterReadings.Requests;
using FluentValidation;

namespace ApiGateway.BusinessLogic.MeterReadings.Validators;

internal sealed class ExportReadingsRequestValidator : AbstractValidator<ExportReadingsRequest>
{
    public ExportReadingsRequestValidator()
    {
        RuleForEach(r => r.Types)
            .IsInEnum();

        RuleForEach(r => r.Locations)
            .IsInEnum();

        RuleFor(r => r.From)
            .LessThan(r => r.To)
            .WithMessage("'From' must be earlier than 'To'.");

        RuleFor(r => r.To)
            .Must((request, to) => to - request.From <= ExportLimits.MaxRange)
            .WithMessage($"'To' must be at most {ExportLimits.MaxRange.Days} days after 'From'.");
    }
}
