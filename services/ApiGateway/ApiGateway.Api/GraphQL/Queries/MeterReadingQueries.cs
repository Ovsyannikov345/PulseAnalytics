using ApiGateway.Api.GraphQL.Errors;
using ApiGateway.Api.GraphQL.Inputs;
using ApiGateway.BusinessLogic.MeterReadings;
using ApiGateway.BusinessLogic.MeterReadings.Models;
using ApiGateway.BusinessLogic.MeterReadings.Requests;
using ApiGateway.BusinessLogic.Paging;
using Ausiannikau.Utilities.Results;
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

        return ToConnection(GetValueOrThrow(result));
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

        return GetValueOrThrow(result);
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

        return GetValueOrThrow(result);
    }

    // A failed result means invalid input, so it becomes a GraphQL error with the BAD_REQUEST code
    // and the result's message.
    private static T GetValueOrThrow<T>(Result<T> result)
    {
        if (result.TryGetSuccessValue(out var value))
        {
            return value;
        }

        throw new GraphQLException(ErrorBuilder.New()
            .SetMessage(result.ErrorMessage)
            .SetCode(GraphQLErrorCodes.BadRequest)
            .Build());
    }

    private static Connection<T> ToConnection<T>(CursorPage<T> page)
    {
        IReadOnlyList<IEdge<T>> edges = [.. page.Items.Select(item => new Edge<T>(item.Item, item.Cursor))];

        var pageInfo = new ConnectionPageInfo(
            page.HasNextPage,
            page.HasPreviousPage,
            startCursor: page.Items.Count > 0 ? page.Items[0].Cursor : null,
            endCursor: page.Items.Count > 0 ? page.Items[^1].Cursor : null);

        return new Connection<T>(edges, pageInfo);
    }
}
