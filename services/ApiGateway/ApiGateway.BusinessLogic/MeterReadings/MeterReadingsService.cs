using ApiGateway.BusinessLogic.MeterReadings.Filtering;
using ApiGateway.BusinessLogic.MeterReadings.Mapping;
using ApiGateway.BusinessLogic.MeterReadings.Models;
using ApiGateway.BusinessLogic.MeterReadings.Requests;
using ApiGateway.BusinessLogic.MeterReadings.Sorting;
using ApiGateway.BusinessLogic.Paging;
using ApiGateway.BusinessLogic.Validation;
using ApiGateway.Common.Constants.Enums;
using ApiGateway.DataAccess.Database;
using ApiGateway.DataAccess.Entities;
using Ausiannikau.Utilities.Results;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace ApiGateway.BusinessLogic.MeterReadings;

public interface IMeterReadingsService
{
    /// <summary>
    /// Returns readings matching the request, newest first, one page at a time.
    /// </summary>
    public Task<Result<CursorPage<MeterReadingModel>>> GetAllReadingsAsync(
        GetAllReadingsRequest request,
        CancellationToken ct);

    /// <summary>
    /// Returns the newest reading of every meter (type and location) matching the request,
    /// ordered by type and location. Meters without readings are left out.
    /// </summary>
    public Task<Result<IReadOnlyList<MeterReadingModel>>> GetLatestReadingsAsync(
        GetLatestReadingsRequest request,
        CancellationToken ct);

    /// <summary>
    /// Counts readings matching the request per group, ordered by type and location.
    /// An empty <see cref="GetReadingCountsRequest.GroupBy"/> returns a single row with the total count.
    /// </summary>
    public Task<Result<IReadOnlyList<ReadingCountModel>>> GetReadingCountsAsync(
        GetReadingCountsRequest request,
        CancellationToken ct);
}

internal sealed class MeterReadingsService(
    IReadOnlyDbContext dbContext,
    IValidator<GetAllReadingsRequest> getAllReadingsRequestValidator,
    IValidator<GetLatestReadingsRequest> getLatestReadingsRequestValidator,
    IValidator<GetReadingCountsRequest> getReadingCountsRequestValidator)
    : IMeterReadingsService
{
    public async Task<Result<CursorPage<MeterReadingModel>>> GetAllReadingsAsync(
        GetAllReadingsRequest request,
        CancellationToken ct)
    {
        var validationResult = await getAllReadingsRequestValidator.ValidateAsync(request, ct);

        if (!validationResult.IsValid)
        {
            return validationResult.ToResult().ToFailureOf<CursorPage<MeterReadingModel>>();
        }

        var query = dbContext.Query<MeterReading>().ApplyFilter(request);

        if (request.Cursor is not null)
        {
            var cursorResult = ReadingCursor.Decode(request.Cursor);

            if (!cursorResult.TryGetSuccessValue(out var cursor))
            {
                return cursorResult.ToFailureOf<CursorPage<MeterReadingModel>>();
            }

            query = query.ApplyCursorPagination(cursor);
        }

        var pageSize = request.PageSize ?? PagingLimits.DefaultPageSize;

        // One extra row tells whether another page follows.
        var readings = await query
            .ApplySort(request)
            .Take(pageSize + 1)
            .Select(r => r.ToModel())
            .ToListAsync(ct);

        var edges = readings
            .Take(pageSize)
            .Select(reading => new CursorEdge<MeterReadingModel>
            {
                Node = reading,
                Cursor = new ReadingCursor(reading.ReceivedAt, reading.Id).Encode()
            })
            .ToList();

        return Result<CursorPage<MeterReadingModel>>.Succeed(new CursorPage<MeterReadingModel>
        {
            Edges = edges,
            HasNextPage = readings.Count > pageSize,
            HasPreviousPage = request.Cursor is not null
        });
    }

    public async Task<Result<IReadOnlyList<MeterReadingModel>>> GetLatestReadingsAsync(
        GetLatestReadingsRequest request,
        CancellationToken ct)
    {
        var validationResult = await getLatestReadingsRequestValidator.ValidateAsync(request, ct);

        if (!validationResult.IsValid)
        {
            return validationResult.ToResult().ToFailureOf<IReadOnlyList<MeterReadingModel>>();
        }

        var readings = await dbContext.Query<MeterReading>()
            .ApplyFilter(request)
            .ApplySort(request)
            .Select(r => r.ToModel())
            .ToListAsync(ct);

        return Result<IReadOnlyList<MeterReadingModel>>.Succeed(readings);
    }

    public async Task<Result<IReadOnlyList<ReadingCountModel>>> GetReadingCountsAsync(
        GetReadingCountsRequest request,
        CancellationToken ct)
    {
        var validationResult = await getReadingCountsRequestValidator.ValidateAsync(request, ct);

        if (!validationResult.IsValid)
        {
            return validationResult.ToResult().ToFailureOf<IReadOnlyList<ReadingCountModel>>();
        }

        var query = dbContext.Query<MeterReading>().ApplyFilter(request);

        var byType = request.GroupBy.Contains(ReadingGrouping.Type);
        var byLocation = request.GroupBy.Contains(ReadingGrouping.Location);

        var groupedCounts = (byType, byLocation) switch
        {
            (true, true) => query
                .GroupBy(r => new { r.Type, r.Location })
                .Select(g => new ReadingCountModel { Type = g.Key.Type, Location = g.Key.Location, Count = g.Count() }),
            (true, false) => query
                .GroupBy(r => r.Type)
                .Select(g => new ReadingCountModel { Type = g.Key, Location = null, Count = g.Count() }),
            (false, true) => query
                .GroupBy(r => r.Location)
                .Select(g => new ReadingCountModel { Type = null, Location = g.Key, Count = g.Count() }),
            (false, false) => null
        };

        if (groupedCounts is null)
        {
            var total = await query.CountAsync(ct);

            return Result<IReadOnlyList<ReadingCountModel>>.Succeed([new ReadingCountModel { Count = total }]);
        }

        var counts = await groupedCounts
            .ApplySort(request)
            .ToListAsync(ct);

        return Result<IReadOnlyList<ReadingCountModel>>.Succeed(counts);
    }
}
