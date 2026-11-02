using Ausiannikau.Utilities.Results;
using DataProcessor.Application.Interfaces.Database;
using DataProcessor.Application.MeterReadings.Models;
using DataProcessor.Application.Telemetry.Metrics;
using DataProcessor.Application.Validation;
using DataProcessor.Domain.Constants.Enums;
using DataProcessor.Domain.Entities.MeterReadings.Payloads;
using FluentValidation;
using Microsoft.Extensions.Logging;

namespace DataProcessor.Application.MeterReadings.Strategies;

internal sealed class MotionReadingProcessingStrategy(
    IApplicationDbContext dbContext,
    IValidator<MotionReadingModel> validator,
    IMetricsService metrics,
    ILogger<MotionReadingProcessingStrategy> logger)
    : ReadingProcessingStrategy<MotionReadingModel>(dbContext, metrics, logger)
{
    protected override MeterType MeterType => MeterType.Motion;

    public override bool CanProcess(MeterReadingModel model) => model is MotionReadingModel;

    protected override async Task<Result> ValidateAsync(MotionReadingModel model, CancellationToken ct)
    {
        var validationResult = await validator.ValidateAsync(model, ct);

        return validationResult.ToResult();
    }

    protected override IReadingPayload CreatePayload(MotionReadingModel model)
    {
        return new MotionPayload
        {
            MotionDetected = model.MotionDetected
        };
    }
}
