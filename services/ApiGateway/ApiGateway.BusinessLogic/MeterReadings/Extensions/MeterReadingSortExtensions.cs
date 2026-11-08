using ApiGateway.BusinessLogic.MeterReadings.Models;
using ApiGateway.BusinessLogic.MeterReadings.Requests;
using ApiGateway.DataAccess.Entities;

namespace ApiGateway.BusinessLogic.MeterReadings.Extensions;

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

    public static IOrderedQueryable<MeterReading> ApplySort(this IQueryable<MeterReading> query, ExportReadingsRequest _)
    {
        return query
            .OrderBy(r => r.ReceivedAt)
            .ThenBy(r => r.Id);
    }

    public static IOrderedQueryable<ReadingCountModel> ApplySort(this IQueryable<ReadingCountModel> query, GetReadingCountsRequest _)
    {
        return query
            .OrderBy(c => c.Type)
            .ThenBy(c => c.Location);
    }

    public static IOrderedQueryable<EnergyStatisticsModel> ApplySort(this IQueryable<EnergyStatisticsModel> query, GetEnergyStatisticsRequest _)
    {
        return query
            .OrderBy(s => s.Location)
            .ThenBy(s => s.BucketStart);
    }

    public static IOrderedQueryable<AirQualityStatisticsModel> ApplySort(this IQueryable<AirQualityStatisticsModel> query, GetAirQualityStatisticsRequest _)
    {
        return query
            .OrderBy(s => s.Location)
            .ThenBy(s => s.BucketStart);
    }

    public static IOrderedQueryable<MotionStatisticsModel> ApplySort(this IQueryable<MotionStatisticsModel> query, GetMotionStatisticsRequest _)
    {
        return query
            .OrderBy(s => s.Location)
            .ThenBy(s => s.BucketStart);
    }
}
