namespace ApiGateway.BusinessLogic.MeterReadings.Constants;

public static class StatisticsLimits
{
    public const int MaxBucketsPerSeries = 1000;

    public static readonly TimeSpan MaxRange = TimeSpan.FromDays(31);
}
