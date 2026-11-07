using ApiGateway.Common.Constants.Enums;

namespace ApiGateway.IntegrationTests.Utilities;

/// <summary>
/// A reading to store before a test (see <see cref="MeterReadingsTable"/>). Built by <see cref="SeedReadings"/>.
/// </summary>
internal sealed record SeedReading
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
