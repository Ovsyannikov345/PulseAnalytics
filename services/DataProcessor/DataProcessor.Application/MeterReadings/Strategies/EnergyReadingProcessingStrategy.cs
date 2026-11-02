using Ausiannikau.Utilities.Results;
using DataProcessor.Application.Interfaces.Database;
using DataProcessor.Application.MeterReadings.Models;
using DataProcessor.Application.Validation;
using DataProcessor.Domain.Constants.Enums;
using DataProcessor.Domain.Entities.MeterReadings.Payloads;
using FluentValidation;
using Microsoft.Extensions.Logging;

namespace DataProcessor.Application.MeterReadings.Strategies;

internal sealed class EnergyReadingProcessingStrategy(
    IApplicationDbContext dbContext,
    IValidator<EnergyReadingModel> validator,
    ILogger<EnergyReadingProcessingStrategy> logger)
    : ReadingProcessingStrategy<EnergyReadingModel>(dbContext, logger)
{
    protected override MeterType MeterType => MeterType.Energy;

    public override bool CanProcess(MeterReadingModel model) => model is EnergyReadingModel;

    protected override async Task<Result> ValidateAsync(EnergyReadingModel model, CancellationToken ct)
    {
        var validationResult = await validator.ValidateAsync(model, ct);

        return validationResult.ToResult();
    }

    protected override IReadingPayload CreatePayload(EnergyReadingModel model)
    {
        return new EnergyPayload
        {
            Energy = model.Energy
        };
    }
}
