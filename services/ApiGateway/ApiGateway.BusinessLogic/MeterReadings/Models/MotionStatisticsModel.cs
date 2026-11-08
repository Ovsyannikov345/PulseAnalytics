using ApiGateway.Common.Constants.Enums;

namespace ApiGateway.BusinessLogic.MeterReadings.Models;

public sealed record MotionStatisticsModel
{
    public MeterLocation? Location { get; init; }

    public DateTimeOffset? BucketStart { get; init; }

    public required int Samples { get; init; }

    public required int MotionSamples { get; init; }

    public double ActivityRatio => Samples != 0 ? (double)MotionSamples / Samples : 0;
}
