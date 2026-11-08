using ApiGateway.Common.Constants.Enums;

namespace ApiGateway.BusinessLogic.MeterReadings.Models;

public sealed record ExportReadingModel
{
    public required Guid Id { get; init; }

    public required MeterType Type { get; init; }

    public required MeterLocation Location { get; init; }

    public required DateTimeOffset ReceivedAt { get; init; }

    public bool? MotionDetected { get; init; }

    public double? Energy { get; init; }

    public int? Co2 { get; init; }

    public int? Pm25 { get; init; }

    public int? Humidity { get; init; }
}
