using Ausiannikau.Utilities.Results;
using DataProcessor.Application.Interfaces.Database;
using DataProcessor.Application.MeterReadings.Models;
using DataProcessor.Application.Validation;
using DataProcessor.Domain.Constants.Enums;
using DataProcessor.Domain.Entities.MeterReadings.Payloads;
using FluentValidation;
using Microsoft.Extensions.Logging;

namespace DataProcessor.Application.MeterReadings.Strategies;

internal sealed class AirQualityReadingProcessingStrategy(
    IApplicationDbContext dbContext,
    IValidator<AirQualityReadingModel> validator,
    ILogger<AirQualityReadingProcessingStrategy> logger)
    : ReadingProcessingStrategy<AirQualityReadingModel>(dbContext, logger)
{
    protected override MeterType MeterType => MeterType.AirQuality;

    public override bool CanProcess(MeterReadingModel model) => model is AirQualityReadingModel;

    protected override async Task<Result> ValidateAsync(AirQualityReadingModel model, CancellationToken ct)
    {
        var validationResult = await validator.ValidateAsync(model, ct);

        return validationResult.ToResult();
    }

    protected override IReadingPayload CreatePayload(AirQualityReadingModel model)
    {
        return new AirQualityPayload
        {
            Co2 = model.Co2,
            Pm25 = model.Pm25,
            Humidity = model.Humidity
        };
    }
}
