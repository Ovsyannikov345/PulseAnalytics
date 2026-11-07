using ApiGateway.Common.Constants.Enums;

namespace ApiGateway.BusinessLogic.MeterReadings.Requests;

public sealed record GetLatestReadingsRequest
{
    public IReadOnlyList<MeterType>? Types { get; init; }

    public IReadOnlyList<MeterLocation>? Locations { get; init; }
}
