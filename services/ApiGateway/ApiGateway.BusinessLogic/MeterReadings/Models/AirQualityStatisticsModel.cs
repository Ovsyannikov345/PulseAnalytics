using ApiGateway.Common.Constants.Enums;

namespace ApiGateway.BusinessLogic.MeterReadings.Models;

public sealed record AirQualityStatisticsModel
{
    public MeterLocation? Location { get; init; }

    public DateTimeOffset? BucketStart { get; init; }

    public required int Samples { get; init; }

    public required MetricStatisticsModel Co2 { get; init; }

    public required MetricStatisticsModel Pm25 { get; init; }

    public required MetricStatisticsModel Humidity { get; init; }
}
