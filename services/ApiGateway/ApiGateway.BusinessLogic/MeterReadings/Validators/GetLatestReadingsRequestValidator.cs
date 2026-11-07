using ApiGateway.BusinessLogic.MeterReadings.Requests;
using FluentValidation;

namespace ApiGateway.BusinessLogic.MeterReadings.Validators;

internal sealed class GetLatestReadingsRequestValidator : AbstractValidator<GetLatestReadingsRequest>
{
    public GetLatestReadingsRequestValidator()
    {
        RuleForEach(r => r.Types)
            .IsInEnum();

        RuleForEach(r => r.Locations)
            .IsInEnum();
    }
}
