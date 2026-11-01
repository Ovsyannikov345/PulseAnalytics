using DataProcessor.Application.Interfaces.Database;
using DataProcessor.Application.MeterReadings.Models;
using DataProcessor.Domain.Constants.Enums;
using DataProcessor.Domain.Entities.MeterReadings.Payloads;
using Microsoft.Extensions.Logging;

namespace DataProcessor.Application.MeterReadings.Strategies;

internal sealed class EnergyReadingProcessingStrategy(
    IApplicationDbContext dbContext,
    ILogger<EnergyReadingProcessingStrategy> logger)
    : ReadingProcessingStrategy<EnergyReadingModel>(dbContext, logger)
{
    protected override MeterType MeterType => MeterType.Energy;

    public override bool CanProcess(MeterReadingModel model) => model is EnergyReadingModel;

    protected override IReadingPayload CreatePayload(EnergyReadingModel model)
    {
        return new EnergyPayload
        {
            Energy = model.Energy
        };
    }
}
