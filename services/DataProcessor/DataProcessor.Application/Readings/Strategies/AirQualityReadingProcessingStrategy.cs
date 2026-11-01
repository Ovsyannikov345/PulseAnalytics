using DataProcessor.Application.Readings.Models;
using DataProcessor.Domain.Constants.Enums;
using DataProcessor.Domain.Entities.Payloads;

namespace DataProcessor.Application.Readings.Strategies;

internal sealed class AirQualityReadingProcessingStrategy : ReadingProcessingStrategy<AirQualityReadingModel>
{
    protected override MeterType MeterType => MeterType.AirQuality;

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
