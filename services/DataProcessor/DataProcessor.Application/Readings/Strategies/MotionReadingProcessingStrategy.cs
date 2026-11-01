using DataProcessor.Application.Readings.Models;
using DataProcessor.Domain.Constants.Enums;
using DataProcessor.Domain.Entities.Payloads;

namespace DataProcessor.Application.Readings.Strategies;

internal sealed class MotionReadingProcessingStrategy : ReadingProcessingStrategy<MotionReadingModel>
{
    protected override MeterType MeterType => MeterType.Motion;

    protected override ReadingPayload CreatePayload(MotionReadingModel model)
    {
        return new MotionPayload
        {
            MotionDetected = model.MotionDetected
        };
    }
}
