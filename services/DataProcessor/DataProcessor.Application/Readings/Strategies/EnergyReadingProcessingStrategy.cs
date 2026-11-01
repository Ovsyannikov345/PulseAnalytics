using DataProcessor.Application.Readings.Models;
using DataProcessor.Domain.Constants.Enums;
using DataProcessor.Domain.Entities.Payloads;

namespace DataProcessor.Application.Readings.Strategies;

internal sealed class EnergyReadingProcessingStrategy : ReadingProcessingStrategy<EnergyReadingModel>
{
    protected override MeterType MeterType => MeterType.Energy;

    protected override ReadingPayload CreatePayload(EnergyReadingModel model)
    {
        return new EnergyPayload
        {
            Energy = model.Energy
        };
    }
}
