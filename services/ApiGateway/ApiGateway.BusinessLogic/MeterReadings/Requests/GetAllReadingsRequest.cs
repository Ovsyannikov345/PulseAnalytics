using ApiGateway.BusinessLogic.Paging;
using ApiGateway.Common.Constants.Enums;

namespace ApiGateway.BusinessLogic.MeterReadings.Requests;

public sealed record GetAllReadingsRequest
{
    public IReadOnlyList<MeterType>? Types { get; init; }

    public IReadOnlyList<MeterLocation>? Locations { get; init; }

    public DateTimeOffset? From { get; init; }

    public DateTimeOffset? To { get; init; }

    public int? PageSize { get; init; }

    public string? Cursor { get; init; }
}
