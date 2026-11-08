namespace ApiGateway.DataAccess.Database.Functions;

public static class DatabaseFunctions
{
    /// <summary>
    /// PostgreSQL <c>date_bin(stride, source, origin)</c>: the start of the <paramref name="stride"/>-long bucket that contains
    /// <paramref name="source"/>, on a grid of buckets aligned with <paramref name="origin"/>. The result is in UTC.
    /// </summary>
    public static DateTimeOffset DateBin(TimeSpan stride, DateTimeOffset source, DateTimeOffset origin)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(stride, TimeSpan.Zero);

        var offsetTicks = (source.UtcTicks - origin.UtcTicks) % stride.Ticks;

        if (offsetTicks < 0)
        {
            offsetTicks += stride.Ticks;
        }

        return new DateTimeOffset(source.UtcTicks - offsetTicks, TimeSpan.Zero);
    }
}
