using ApiGateway.BusinessLogic.MeterReadings.Models;
using ApiGateway.BusinessLogic.MeterReadings.Requests;
using ApiGateway.DataAccess.Entities;

namespace ApiGateway.BusinessLogic.MeterReadings.Sorting;

internal static class MeterReadingSortExtensions
{
    public static IOrderedQueryable<MeterReading> ApplySort(this IQueryable<MeterReading> query, GetAllReadingsRequest _)
    {
        return query
            .OrderByDescending(r => r.ReceivedAt)
            .ThenByDescending(r => r.Id);
    }

    public static IOrderedQueryable<MeterReading> ApplySort(this IQueryable<MeterReading> query, GetLatestReadingsRequest _)
    {
        return query
            .OrderBy(r => r.Type)
            .ThenBy(r => r.Location);
    }

    public static IOrderedQueryable<ReadingCountModel> ApplySort(this IQueryable<ReadingCountModel> query, GetReadingCountsRequest _)
    {
        return query
            .OrderBy(c => c.Type)
            .ThenBy(c => c.Location);
    }
}
