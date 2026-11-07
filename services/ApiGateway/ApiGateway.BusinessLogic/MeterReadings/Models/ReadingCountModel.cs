using ApiGateway.Common.Constants.Enums;

namespace ApiGateway.BusinessLogic.MeterReadings.Models;

public sealed record ReadingCountModel
{
    public MeterType? Type { get; init; }

    public MeterLocation? Location { get; init; }

    public required int Count { get; init; }
}
