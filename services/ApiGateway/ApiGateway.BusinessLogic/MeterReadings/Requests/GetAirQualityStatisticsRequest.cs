using ApiGateway.Common.Constants.Enums;

namespace ApiGateway.BusinessLogic.MeterReadings.Requests;

public sealed record GetAirQualityStatisticsRequest
{
    public IReadOnlyList<MeterLocation>? Locations { get; init; }

    public required DateTimeOffset From { get; init; }

    public required DateTimeOffset To { get; init; }

    public IReadOnlyList<StatisticsGrouping> GroupBy { get; init; } = [];

    public TimeBucket? Bucket { get; init; }
}
