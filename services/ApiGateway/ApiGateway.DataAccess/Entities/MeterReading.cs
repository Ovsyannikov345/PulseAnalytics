using ApiGateway.Common.Constants.Enums;

namespace ApiGateway.DataAccess.Entities;

/// <summary>
/// A row of the processor's <c>reporting.meter_readings</c> view. Only the columns of the reading's own type are set.
/// </summary>
public sealed class MeterReading : BaseEntity
{
    public required MeterType Type { get; init; }

    public required MeterLocation Location { get; init; }

    public required DateTimeOffset ReceivedAt { get; init; }

    public bool? MotionDetected { get; init; }

    public double? Energy { get; init; }

    public int? Co2 { get; init; }

    public int? Pm25 { get; init; }

    public int? Humidity { get; init; }
}
