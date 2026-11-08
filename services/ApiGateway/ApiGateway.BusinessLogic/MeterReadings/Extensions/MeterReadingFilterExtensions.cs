using ApiGateway.BusinessLogic.MeterReadings.Requests;
using ApiGateway.BusinessLogic.Paging;
using ApiGateway.Common.Constants.Enums;
using ApiGateway.DataAccess.Entities;

namespace ApiGateway.BusinessLogic.MeterReadings.Extensions;

internal static class MeterReadingFilterExtensions
{
    private static readonly MeterType[] AllTypes = Enum.GetValues<MeterType>();

    private static readonly MeterLocation[] AllLocations = Enum.GetValues<MeterLocation>();

    public static IQueryable<MeterReading> ApplyFilter(this IQueryable<MeterReading> query, GetAllReadingsRequest request)
    {
        return query
            .WhereTypeIn(request.Types)
            .WhereLocationIn(request.Locations)
            .WhereReceivedWithin(request.From, request.To);
    }

    public static IQueryable<MeterReading> ApplyFilter(this IQueryable<MeterReading> query, GetReadingCountsRequest request)
    {
        return query
            .WhereTypeIn(request.Types)
            .WhereLocationIn(request.Locations)
            .WhereReceivedWithin(request.From, request.To);
    }

    public static IQueryable<MeterReading> ApplyFilter(this IQueryable<MeterReading> query, GetLatestReadingsRequest request)
    {
        var types = request.Types is { Count: > 0 }
            ? [.. request.Types.Distinct()]
            : AllTypes;

        var locations = request.Locations is { Count: > 0 }
            ? [.. request.Locations.Distinct()]
            : AllLocations;

        return types
            .SelectMany(_ => locations, (type, location) => query
                .Where(r => r.Type == type && r.Location == location)
                .OrderByDescending(r => r.ReceivedAt)
                .ThenByDescending(r => r.Id)
                .Take(1))
            .Aggregate(Queryable.Concat);
    }

    public static IQueryable<MeterReading> ApplyFilter(this IQueryable<MeterReading> query, GetEnergyStatisticsRequest request)
    {
        return query
            .Where(r => r.Type == MeterType.Energy)
            .WhereLocationIn(request.Locations)
            .WhereReceivedWithin(request.From, request.To);
    }

    public static IQueryable<MeterReading> ApplyFilter(this IQueryable<MeterReading> query, GetAirQualityStatisticsRequest request)
    {
        return query
            .Where(r => r.Type == MeterType.AirQuality)
            .WhereLocationIn(request.Locations)
            .WhereReceivedWithin(request.From, request.To);
    }

    public static IQueryable<MeterReading> ApplyFilter(this IQueryable<MeterReading> query, GetMotionStatisticsRequest request)
    {
        return query
            .Where(r => r.Type == MeterType.Motion)
            .WhereLocationIn(request.Locations)
            .WhereReceivedWithin(request.From, request.To);
    }

    public static IQueryable<MeterReading> ApplyCursorPagination(this IQueryable<MeterReading> query, ReadingCursor cursor)
    {
        var receivedAt = cursor.ReceivedAt;

        var id = cursor.Id;

        // The first condition is implied by the second one, but gives Postgres a range on the received_at index
        return query.Where(r => r.ReceivedAt <= receivedAt && (r.ReceivedAt < receivedAt || r.Id.CompareTo(id) < 0));
    }

    private static IQueryable<MeterReading> WhereTypeIn(this IQueryable<MeterReading> query, IReadOnlyList<MeterType>? types)
    {
        return types is { Count: > 0 }
            ? query.Where(r => types.Contains(r.Type))
            : query;
    }

    private static IQueryable<MeterReading> WhereLocationIn(this IQueryable<MeterReading> query, IReadOnlyList<MeterLocation>? locations)
    {
        return locations is { Count: > 0 }
            ? query.Where(r => locations.Contains(r.Location))
            : query;
    }

    private static IQueryable<MeterReading> WhereReceivedWithin(this IQueryable<MeterReading> query, DateTimeOffset? from, DateTimeOffset? to)
    {
        if (from is { } fromValue)
        {
            var fromUtc = fromValue.ToUniversalTime();

            query = query.Where(r => r.ReceivedAt >= fromUtc);
        }

        if (to is { } toValue)
        {
            var toUtc = toValue.ToUniversalTime();

            query = query.Where(r => r.ReceivedAt < toUtc);
        }

        return query;
    }
}
