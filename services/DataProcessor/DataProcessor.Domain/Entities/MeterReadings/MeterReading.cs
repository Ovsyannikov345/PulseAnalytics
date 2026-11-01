using DataProcessor.Domain.Constants.Enums;
using DataProcessor.Domain.Entities.MeterReadings.Payloads;

namespace DataProcessor.Domain.Entities.MeterReadings;

public sealed class MeterReading : BaseEntity
{
    public required Guid DeduplicationKey { get; init; }

    public required MeterType Type { get; init; }

    public required MeterLocation Location { get; init; }

    public required DateTimeOffset ReceivedAt { get; init; }

    public required IReadingPayload Payload { get; init; }
}
