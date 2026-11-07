using ApiGateway.Api.GraphQL.Errors;
using ApiGateway.Api.GraphQL.Inputs;
using ApiGateway.Api.GraphQL.Paging;
using ApiGateway.BusinessLogic.MeterReadings;
using ApiGateway.BusinessLogic.MeterReadings.Models;
using ApiGateway.BusinessLogic.MeterReadings.Requests;
using ApiGateway.BusinessLogic.Paging;
using HotChocolate;
using HotChocolate.Language;
using HotChocolate.Types;
using HotChocolate.Types.Pagination;

namespace ApiGateway.Api.GraphQL.Queries;

[ExtendObjectType(OperationType.Query)]
internal sealed class MeterReadingQueries
{
    // HotChocolate declares and checks the paging arguments; the service pages the query itself (keyset on
    // ReceivedAt + Id), and the returned connection is passed through as is.
    [UsePaging(
        ConnectionName = "MeterReading",
        AllowBackwardPagination = false,
        IncludeTotalCount = false,
        DefaultPageSize = PagingLimits.DefaultPageSize,
        MaxPageSize = PagingLimits.MaxPageSize)]
    [GraphQLDescription("Stored readings, newest first.")]
    public async Task<Connection<MeterReadingModel>> GetReadingsAsync(
        ReadingFilterInput? filter,
        int? first,
        string? after,
        IMeterReadingsService meterReadingsService,
        CancellationToken ct)
    {
        var request = new GetAllReadingsRequest
        {
            Types = filter?.Types,
            Locations = filter?.Locations,
            From = filter?.From,
            To = filter?.To,
            PageSize = first,
            Cursor = after
        };

        var result = await meterReadingsService.GetAllReadingsAsync(request, ct);

        return result.GetValueOrThrow().ToConnection();
    }

    [GraphQLDescription("The newest reading of every meter (type and location), ordered by type and location.")]
    public async Task<IReadOnlyList<MeterReadingModel>> GetLatestReadingsAsync(
        LatestReadingsFilterInput? filter,
        IMeterReadingsService meterReadingsService,
        CancellationToken ct)
    {
        var request = new GetLatestReadingsRequest
        {
            Types = filter?.Types,
            Locations = filter?.Locations
        };

        var result = await meterReadingsService.GetLatestReadingsAsync(request, ct);

        return result.GetValueOrThrow();
    }

    [GraphQLDescription("The number of readings per group. An empty `groupBy` returns the total count.")]
    public async Task<IReadOnlyList<ReadingCountModel>> GetReadingCountsAsync(
        ReadingFilterInput? filter,
        IReadOnlyList<ReadingGrouping> groupBy,
        IMeterReadingsService meterReadingsService,
        CancellationToken ct)
    {
        var request = new GetReadingCountsRequest
        {
            Types = filter?.Types,
            Locations = filter?.Locations,
            From = filter?.From,
            To = filter?.To,
            GroupBy = groupBy
        };

        var result = await meterReadingsService.GetReadingCountsAsync(request, ct);

        return result.GetValueOrThrow();
    }
}
