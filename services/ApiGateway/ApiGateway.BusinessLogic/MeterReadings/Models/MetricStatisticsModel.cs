namespace ApiGateway.BusinessLogic.MeterReadings.Models;

public sealed record MetricStatisticsModel
{
    public required double Average { get; init; }

    public required double Minimum { get; init; }

    public required double Maximum { get; init; }
}
