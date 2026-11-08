using ApiGateway.Common.Constants.Enums;

namespace ApiGateway.BusinessLogic.MeterReadings.Requests;

public sealed record ExportReadingsRequest
{
    public IReadOnlyList<MeterType>? Types { get; init; }

    public IReadOnlyList<MeterLocation>? Locations { get; init; }

    public required DateTimeOffset From { get; init; }

    public required DateTimeOffset To { get; init; }
}
