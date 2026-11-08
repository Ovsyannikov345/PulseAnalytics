using ApiGateway.BusinessLogic.MeterReadings.Requests;
using ApiGateway.BusinessLogic.Statistics;
using ApiGateway.Common.Constants.Enums;
using FluentValidation;

namespace ApiGateway.BusinessLogic.MeterReadings.Validators;

internal sealed class GetEnergyStatisticsRequestValidator : AbstractValidator<GetEnergyStatisticsRequest>
{
    public GetEnergyStatisticsRequestValidator()
    {
        RuleForEach(r => r.Locations)
            .IsInEnum();

        RuleFor(r => r.From)
            .LessThan(r => r.To)
            .WithMessage("'From' must be earlier than 'To'.");

        RuleFor(r => r.To)
            .Must((request, to) => to - request.From <= StatisticsLimits.MaxRange)
            .WithMessage($"'To' must be at most {StatisticsLimits.MaxRange.Days} days after 'From'.");

        RuleForEach(r => r.GroupBy)
            .IsInEnum();

        RuleFor(r => r.Bucket)
            .IsInEnum();

        RuleFor(r => r.Bucket)
            .NotNull()
            .When(r => r.GroupBy.Contains(StatisticsGrouping.Time))
            .WithMessage("'Bucket' is required when grouping by time.");

        RuleFor(r => r.Bucket)
            .Null()
            .When(r => !r.GroupBy.Contains(StatisticsGrouping.Time))
            .WithMessage("'Bucket' must be empty unless grouping by time.");

        RuleFor(r => r.Bucket)
            .Must((request, bucket) => request.To - request.From <= bucket!.Value.ToTimeSpan() * StatisticsLimits.MaxBucketsPerSeries)
            .When(r => r.Bucket is { } bucket && Enum.IsDefined(bucket))
            .WithMessage($"'Bucket' must split the range into at most {StatisticsLimits.MaxBucketsPerSeries} buckets.");
    }
}
