using ApiGateway.Common.Constants.Enums;

namespace ApiGateway.BusinessLogic.MeterReadings.Models;

public sealed record EnergyStatisticsModel
{
    public MeterLocation? Location { get; init; }

    public DateTimeOffset? BucketStart { get; init; }

    public required int Samples { get; init; }

    public required MetricStatisticsModel Energy { get; init; }
}
