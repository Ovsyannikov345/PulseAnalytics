using ApiGateway.Common.Constants.Enums;

namespace ApiGateway.BusinessLogic.MeterReadings.Models;

public sealed record MotionReadingModel : MeterReadingModel
{
    public override MeterType Type => MeterType.Motion;

    public required bool MotionDetected { get; init; }
}
