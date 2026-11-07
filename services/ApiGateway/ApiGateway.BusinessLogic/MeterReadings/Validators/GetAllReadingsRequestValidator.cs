using ApiGateway.BusinessLogic.MeterReadings.Requests;
using ApiGateway.BusinessLogic.Paging;
using FluentValidation;

namespace ApiGateway.BusinessLogic.MeterReadings.Validators;

internal sealed class GetAllReadingsRequestValidator : AbstractValidator<GetAllReadingsRequest>
{
    public GetAllReadingsRequestValidator()
    {
        RuleForEach(r => r.Types)
            .IsInEnum();

        RuleForEach(r => r.Locations)
            .IsInEnum();

        RuleFor(r => r.From)
            .LessThan(r => r.To)
            .When(r => r.From.HasValue && r.To.HasValue)
            .WithMessage("'From' must be earlier than 'To'.");

        RuleFor(r => r.PageSize)
            .InclusiveBetween(1, PagingLimits.MaxPageSize);
    }
}
