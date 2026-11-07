using ApiGateway.BusinessLogic.MeterReadings.Requests;
using FluentValidation;

namespace ApiGateway.BusinessLogic.MeterReadings.Validators;

internal sealed class GetReadingCountsRequestValidator : AbstractValidator<GetReadingCountsRequest>
{
    public GetReadingCountsRequestValidator()
    {
        RuleForEach(r => r.Types)
            .IsInEnum();

        RuleForEach(r => r.Locations)
            .IsInEnum();

        RuleFor(r => r.From)
            .LessThan(r => r.To)
            .When(r => r.From.HasValue && r.To.HasValue)
            .WithMessage("'From' must be earlier than 'To'.");

        RuleForEach(r => r.GroupBy)
            .IsInEnum();
    }
}
