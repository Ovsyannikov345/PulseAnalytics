using DataIngestor.Domain.Constants.Enums;

namespace DataIngestor.Domain.Entities;

public sealed record MeterReading
{
    public required string Name { get; init; }

    public required MeterType Type { get; init; }

    public required DateTimeOffset ReceivedAt { get; init; }

    public required IReadingPayload Payload { get; init; }
}
