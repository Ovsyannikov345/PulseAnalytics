using ApiGateway.BusinessLogic.MeterReadings.Extensions;
using ApiGateway.BusinessLogic.MeterReadings.Models;
using ApiGateway.BusinessLogic.MeterReadings.Requests;
using ApiGateway.BusinessLogic.Validation;
using ApiGateway.DataAccess.Database;
using ApiGateway.DataAccess.Entities;
using Ausiannikau.Utilities.Results;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace ApiGateway.BusinessLogic.MeterReadings;

public interface IReadingStatisticsService
{
    /// <summary>
    /// Returns energy statistics of the readings matching the request per group, ordered by location and bucket start.
    /// Groups without readings are left out.
    /// </summary>
    public Task<Result<IReadOnlyList<EnergyStatisticsModel>>> GetEnergyStatisticsAsync(
        GetEnergyStatisticsRequest request,
        CancellationToken ct);

    /// <summary>
    /// Returns air quality statistics of the readings matching the request per group, ordered by location and bucket start.
    /// Groups without readings are left out.
    /// </summary>
    public Task<Result<IReadOnlyList<AirQualityStatisticsModel>>> GetAirQualityStatisticsAsync(
        GetAirQualityStatisticsRequest request,
        CancellationToken ct);

    /// <summary>
    /// Returns motion statistics of the readings matching the request per group, ordered by location and bucket start.
    /// Groups without readings are left out.
    /// </summary>
    public Task<Result<IReadOnlyList<MotionStatisticsModel>>> GetMotionStatisticsAsync(
        GetMotionStatisticsRequest request,
        CancellationToken ct);
}

internal sealed class ReadingStatisticsService(
    IReadOnlyDbContext dbContext,
    IValidator<GetEnergyStatisticsRequest> getEnergyStatisticsRequestValidator,
    IValidator<GetAirQualityStatisticsRequest> getAirQualityStatisticsRequestValidator,
    IValidator<GetMotionStatisticsRequest> getMotionStatisticsRequestValidator)
    : IReadingStatisticsService
{
    public async Task<Result<IReadOnlyList<EnergyStatisticsModel>>> GetEnergyStatisticsAsync(
        GetEnergyStatisticsRequest request,
        CancellationToken ct)
    {
        var validationResult = await getEnergyStatisticsRequestValidator.ValidateAsync(request, ct);

        if (!validationResult.IsValid)
        {
            return validationResult.ToResult().ToFailureOf<IReadOnlyList<EnergyStatisticsModel>>();
        }

        var statistics = await dbContext.Query<MeterReading>()
            .ApplyFilter(request)
            .GroupByStatisticsKey(request.GroupBy, request.Bucket)
            .Select(g => new EnergyStatisticsModel
            {
                Location = g.Key.Location,
                BucketStart = g.Key.BucketStart,
                Samples = g.Count(),
                Energy = new MetricStatisticsModel
                {
                    Average = g.Average(r => r.Energy!.Value),
                    Minimum = g.Min(r => r.Energy!.Value),
                    Maximum = g.Max(r => r.Energy!.Value)
                }
            })
            .ApplySort(request)
            .ToListAsync(ct);

        return Result<IReadOnlyList<EnergyStatisticsModel>>.Succeed(statistics);
    }

    public async Task<Result<IReadOnlyList<AirQualityStatisticsModel>>> GetAirQualityStatisticsAsync(
        GetAirQualityStatisticsRequest request,
        CancellationToken ct)
    {
        var validationResult = await getAirQualityStatisticsRequestValidator.ValidateAsync(request, ct);

        if (!validationResult.IsValid)
        {
            return validationResult.ToResult().ToFailureOf<IReadOnlyList<AirQualityStatisticsModel>>();
        }

        var statistics = await dbContext.Query<MeterReading>()
            .ApplyFilter(request)
            .GroupByStatisticsKey(request.GroupBy, request.Bucket)
            .Select(g => new AirQualityStatisticsModel
            {
                Location = g.Key.Location,
                BucketStart = g.Key.BucketStart,
                Samples = g.Count(),
                Co2 = new MetricStatisticsModel
                {
                    Average = g.Average(r => r.Co2!.Value),
                    Minimum = g.Min(r => r.Co2!.Value),
                    Maximum = g.Max(r => r.Co2!.Value)
                },
                Pm25 = new MetricStatisticsModel
                {
                    Average = g.Average(r => r.Pm25!.Value),
                    Minimum = g.Min(r => r.Pm25!.Value),
                    Maximum = g.Max(r => r.Pm25!.Value)
                },
                Humidity = new MetricStatisticsModel
                {
                    Average = g.Average(r => r.Humidity!.Value),
                    Minimum = g.Min(r => r.Humidity!.Value),
                    Maximum = g.Max(r => r.Humidity!.Value)
                }
            })
            .ApplySort(request)
            .ToListAsync(ct);

        return Result<IReadOnlyList<AirQualityStatisticsModel>>.Succeed(statistics);
    }

    public async Task<Result<IReadOnlyList<MotionStatisticsModel>>> GetMotionStatisticsAsync(
        GetMotionStatisticsRequest request,
        CancellationToken ct)
    {
        var validationResult = await getMotionStatisticsRequestValidator.ValidateAsync(request, ct);

        if (!validationResult.IsValid)
        {
            return validationResult.ToResult().ToFailureOf<IReadOnlyList<MotionStatisticsModel>>();
        }

        var statistics = await dbContext.Query<MeterReading>()
            .ApplyFilter(request)
            .GroupByStatisticsKey(request.GroupBy, request.Bucket)
            .Select(g => new MotionStatisticsModel
            {
                Location = g.Key.Location,
                BucketStart = g.Key.BucketStart,
                Samples = g.Count(),
                MotionSamples = g.Count(r => r.MotionDetected == true)
            })
            .ApplySort(request)
            .ToListAsync(ct);

        return Result<IReadOnlyList<MotionStatisticsModel>>.Succeed(statistics);
    }
}
