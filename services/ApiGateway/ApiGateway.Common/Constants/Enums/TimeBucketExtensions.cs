namespace ApiGateway.Common.Constants.Enums;

public static class TimeBucketExtensions
{
    public static TimeSpan ToTimeSpan(this TimeBucket bucket)
    {
        return bucket switch
        {
            TimeBucket.Minute => TimeSpan.FromMinutes(1),
            TimeBucket.FiveMinutes => TimeSpan.FromMinutes(5),
            TimeBucket.FifteenMinutes => TimeSpan.FromMinutes(15),
            TimeBucket.Hour => TimeSpan.FromHours(1),
            TimeBucket.Day => TimeSpan.FromDays(1),
            _ => throw new ArgumentOutOfRangeException(nameof(bucket), bucket, "Unknown time bucket."),
        };
    }
}
