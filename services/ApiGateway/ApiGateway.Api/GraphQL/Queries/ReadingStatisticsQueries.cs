using ApiGateway.Api.GraphQL.Errors;
using ApiGateway.Api.GraphQL.Inputs;
using ApiGateway.Api.GraphQL.Types.Statistics;
using ApiGateway.BusinessLogic.MeterReadings;
using ApiGateway.BusinessLogic.MeterReadings.Models;
using ApiGateway.BusinessLogic.MeterReadings.Requests;
using ApiGateway.Common.Constants.Enums;
using HotChocolate;
using HotChocolate.Language;
using HotChocolate.Types;

namespace ApiGateway.Api.GraphQL.Queries;

[ExtendObjectType(OperationType.Query)]
internal static class ReadingStatisticsQueries
{
    private const string GroupByDescription = "Groups to aggregate by. An empty list aggregates all matching readings into one row.";

    private const string BucketDescription = "Size of the time buckets on a fixed UTC grid; required when grouping by TIME and not allowed otherwise.";

    [GraphQLDescription("Energy consumption statistics per group, ordered by location and bucket start. Groups without readings are left out.")]
    public static async Task<IReadOnlyList<EnergyStatistics>> GetEnergyStatisticsAsync(
        StatisticsFilterInput filter,
        [GraphQLDescription(GroupByDescription)] IReadOnlyList<StatisticsGrouping> groupBy,
        [GraphQLDescription(BucketDescription)] TimeBucket? bucket,
        IReadingStatisticsService readingStatisticsService,
        CancellationToken ct)
    {
        var request = new GetEnergyStatisticsRequest
        {
            Locations = filter.Locations,
            From = filter.From,
            To = filter.To,
            GroupBy = groupBy,
            Bucket = bucket
        };

        var result = await readingStatisticsService.GetEnergyStatisticsAsync(request, ct);

        return [.. result.GetValueOrThrow().Select(ToEnergyStatistics)];
    }

    [GraphQLDescription("Air quality statistics per group, ordered by location and bucket start. Groups without readings are left out.")]
    public static async Task<IReadOnlyList<AirQualityStatistics>> GetAirQualityStatisticsAsync(
        StatisticsFilterInput filter,
        [GraphQLDescription(GroupByDescription)] IReadOnlyList<StatisticsGrouping> groupBy,
        [GraphQLDescription(BucketDescription)] TimeBucket? bucket,
        IReadingStatisticsService readingStatisticsService,
        CancellationToken ct)
    {
        var request = new GetAirQualityStatisticsRequest
        {
            Locations = filter.Locations,
            From = filter.From,
            To = filter.To,
            GroupBy = groupBy,
            Bucket = bucket
        };

        var result = await readingStatisticsService.GetAirQualityStatisticsAsync(request, ct);

        return [.. result.GetValueOrThrow().Select(ToAirQualityStatistics)];
    }

    [GraphQLDescription("Motion activity statistics per group, ordered by location and bucket start. Groups without readings are left out.")]
    public static async Task<IReadOnlyList<MotionStatistics>> GetMotionStatisticsAsync(
        StatisticsFilterInput filter,
        [GraphQLDescription(GroupByDescription)] IReadOnlyList<StatisticsGrouping> groupBy,
        [GraphQLDescription(BucketDescription)] TimeBucket? bucket,
        IReadingStatisticsService readingStatisticsService,
        CancellationToken ct)
    {
        var request = new GetMotionStatisticsRequest
        {
            Locations = filter.Locations,
            From = filter.From,
            To = filter.To,
            GroupBy = groupBy,
            Bucket = bucket
        };

        var result = await readingStatisticsService.GetMotionStatisticsAsync(request, ct);

        return [.. result.GetValueOrThrow().Select(ToMotionStatistics)];
    }

    private static EnergyStatistics ToEnergyStatistics(EnergyStatisticsModel model)
    {
        return new EnergyStatistics
        {
            Location = model.Location,
            BucketStart = model.BucketStart,
            Samples = model.Samples,
            Energy = ToMetricStatistics(model.Energy)
        };
    }

    private static AirQualityStatistics ToAirQualityStatistics(AirQualityStatisticsModel model)
    {
        return new AirQualityStatistics
        {
            Location = model.Location,
            BucketStart = model.BucketStart,
            Samples = model.Samples,
            Co2 = ToMetricStatistics(model.Co2),
            Pm25 = ToMetricStatistics(model.Pm25),
            Humidity = ToMetricStatistics(model.Humidity)
        };
    }

    private static MetricStatistics ToMetricStatistics(MetricStatisticsModel model)
    {
        return new MetricStatistics
        {
            Average = model.Average,
            Minimum = model.Minimum,
            Maximum = model.Maximum
        };
    }

    private static MotionStatistics ToMotionStatistics(MotionStatisticsModel model)
    {
        return new MotionStatistics
        {
            Location = model.Location,
            BucketStart = model.BucketStart,
            Samples = model.Samples,
            MotionSamples = model.MotionSamples,
            ActivityRatio = model.ActivityRatio
        };
    }
}
