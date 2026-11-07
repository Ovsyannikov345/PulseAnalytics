using ApiGateway.Api.GraphQL.Errors;
using ApiGateway.Api.GraphQL.Inputs;
using ApiGateway.Api.GraphQL.Readings;
using ApiGateway.BusinessLogic.MeterReadings;
using ApiGateway.BusinessLogic.MeterReadings.Models;
using ApiGateway.BusinessLogic.MeterReadings.Requests;
using ApiGateway.BusinessLogic.Paging;
using ApiGateway.Common.Constants.Enums;
using Ausiannikau.Utilities.Results;
using HotChocolate;
using HotChocolate.Language;
using HotChocolate.Types;
using HotChocolate.Types.Pagination;

namespace ApiGateway.Api.GraphQL.Queries;

[ExtendObjectType(OperationType.Query)]
internal static class MeterReadingQueries
{
    [UsePaging(
        ConnectionName = "MeterReading",
        AllowBackwardPagination = false,
        IncludeTotalCount = false,
        DefaultPageSize = PagingLimits.DefaultPageSize,
        MaxPageSize = PagingLimits.MaxPageSize)]
    [GraphQLDescription("Stored readings, newest first.")]
    public static async Task<Connection<MeterReading>> GetReadingsAsync(
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
    public static async Task<IReadOnlyList<MeterReading>> GetLatestReadingsAsync(
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

        return [.. GetValueOrThrow(result).Select(ToMeterReading)];
    }

    [GraphQLDescription("The number of readings per group. An empty `groupBy` returns the total count.")]
    public static async Task<IReadOnlyList<ReadingCount>> GetReadingCountsAsync(
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

        return [.. GetValueOrThrow(result).Select(ToReadingCount)];
    }

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

    private static Connection<MeterReading> ToConnection(CursorPage<MeterReadingModel> page)
    {
        IReadOnlyList<IEdge<MeterReading>> edges =
            [.. page.Items.Select(item => new Edge<MeterReading>(ToMeterReading(item.Item), item.Cursor))];

        var pageInfo = new ConnectionPageInfo(
            page.HasNextPage,
            page.HasPreviousPage,
            startCursor: page.Items.Count > 0 ? page.Items[0].Cursor : null,
            endCursor: page.Items.Count > 0 ? page.Items[^1].Cursor : null);

        return new Connection<MeterReading>(edges, pageInfo);
    }

    private static MeterReading ToMeterReading(MeterReadingModel model)
    {
        return model switch
        {
            MotionReadingModel motion => new MotionReading
            {
                Id = motion.Id,
                Location = motion.Location,
                ReceivedAt = motion.ReceivedAt,
                MotionDetected = motion.MotionDetected
            },
            EnergyReadingModel energy => new EnergyReading
            {
                Id = energy.Id,
                Location = energy.Location,
                ReceivedAt = energy.ReceivedAt,
                Energy = energy.Energy
            },
            AirQualityReadingModel airQuality => new AirQualityReading
            {
                Id = airQuality.Id,
                Location = airQuality.Location,
                ReceivedAt = airQuality.ReceivedAt,
                Co2 = airQuality.Co2,
                Pm25 = airQuality.Pm25,
                Humidity = airQuality.Humidity
            },
            _ => throw new ArgumentOutOfRangeException(nameof(model), model.GetType().Name, "Unknown reading model.")
        };
    }

    private static ReadingCount ToReadingCount(ReadingCountModel model)
    {
        return new ReadingCount
        {
            Type = model.Type,
            Location = model.Location,
            Count = model.Count
        };
    }
}
