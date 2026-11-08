using ApiGateway.Common.Constants.Enums;
using ApiGateway.DataAccess.Database.Functions;
using ApiGateway.DataAccess.Entities;

namespace ApiGateway.BusinessLogic.MeterReadings.Extensions;

internal static class StatisticsGroupingExtensions
{
    /// <summary>
    /// Start of the fixed UTC grid of time buckets: buckets of each size follow each other from the Unix epoch, so hours
    /// start on the hour and days at UTC midnight, and a bucket doesn't move when the requested range does.
    /// </summary>
    private static readonly DateTimeOffset BucketOrigin = DateTimeOffset.UnixEpoch;

    /// <summary>
    /// Groups readings by location and/or by time bucket of size <paramref name="bucket"/> on the <see cref="BucketOrigin"/>
    /// grid, as <paramref name="groupBy"/> lists.
    /// </summary>
    public static IQueryable<IGrouping<StatisticsGroupKey, MeterReading>> GroupByStatisticsKey(
        this IQueryable<MeterReading> query,
        IReadOnlyList<StatisticsGrouping> groupBy,
        TimeBucket? bucket)
    {
        var byLocation = groupBy.Contains(StatisticsGrouping.Location);

        if (!groupBy.Contains(StatisticsGrouping.Time))
        {
            return byLocation
                ? query.GroupBy(r => new StatisticsGroupKey { Location = r.Location, BucketStart = null })
                : query.GroupBy(r => new StatisticsGroupKey { Location = null, BucketStart = null });
        }

        if (bucket?.ToTimeSpan() is not { } bucketLength)
        {
            throw new ArgumentException("Grouping by time requires a bucket.", nameof(bucket));
        }

        return byLocation
            ? query.GroupBy(r => new StatisticsGroupKey
            {
                Location = r.Location,
                BucketStart = DatabaseFunctions.DateBin(bucketLength, r.ReceivedAt, BucketOrigin)
            })
            : query.GroupBy(r => new StatisticsGroupKey
            {
                Location = null,
                BucketStart = DatabaseFunctions.DateBin(bucketLength, r.ReceivedAt, BucketOrigin)
            });
    }
}

internal sealed record StatisticsGroupKey
{
    public MeterLocation? Location { get; init; }

    public DateTimeOffset? BucketStart { get; init; }
}
