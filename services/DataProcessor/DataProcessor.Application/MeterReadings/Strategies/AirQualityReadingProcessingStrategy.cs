using DataProcessor.Application.Interfaces.Database;
using DataProcessor.Application.MeterReadings.Models;
using DataProcessor.Domain.Constants.Enums;
using DataProcessor.Domain.Entities.MeterReadings.Payloads;
using Microsoft.Extensions.Logging;

namespace DataProcessor.Application.MeterReadings.Strategies;

internal sealed class AirQualityReadingProcessingStrategy(
    IApplicationDbContext dbContext,
    ILogger<AirQualityReadingProcessingStrategy> logger)
    : ReadingProcessingStrategy<AirQualityReadingModel>(dbContext, logger)
{
    protected override MeterType MeterType => MeterType.AirQuality;

    public override bool CanProcess(MeterReadingModel model) => model is AirQualityReadingModel;

    protected override ReadingPayload CreatePayload(AirQualityReadingModel model)
    {
        return new AirQualityPayload
        {
            Co2 = model.Co2,
            Pm25 = model.Pm25,
            Humidity = model.Humidity
        };
    }
}
