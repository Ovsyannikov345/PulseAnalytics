using ApiGateway.Common.Constants.Enums;

namespace ApiGateway.BusinessLogic.MeterReadings.Models;

public abstract record MeterReadingModel
{
    public required Guid Id { get; init; }

    public abstract MeterType Type { get; }

    public required MeterLocation Location { get; init; }

    public required DateTimeOffset ReceivedAt { get; init; }
}
