using DataProcessor.Application.Interfaces.Database;
using DataProcessor.Application.MeterReadings.Models;
using DataProcessor.Domain.Constants.Enums;
using DataProcessor.Domain.Entities.MeterReadings.Payloads;
using Microsoft.Extensions.Logging;

namespace DataProcessor.Application.MeterReadings.Strategies;

internal sealed class MotionReadingProcessingStrategy(
    IApplicationDbContext dbContext,
    ILogger<AirQualityReadingProcessingStrategy> logger)
    : ReadingProcessingStrategy<MotionReadingModel>(dbContext, logger)
{
    protected override MeterType MeterType => MeterType.Motion;

    public override bool CanProcess(MeterReadingModel model) => model is MotionReadingModel;

    protected override ReadingPayload CreatePayload(MotionReadingModel model)
    {
        return new MotionPayload
        {
            MotionDetected = model.MotionDetected
        };
    }
}
