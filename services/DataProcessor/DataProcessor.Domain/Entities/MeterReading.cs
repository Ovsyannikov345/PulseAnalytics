using DataProcessor.Domain.Constants.Enums;
using DataProcessor.Domain.Entities.Payloads;

namespace DataProcessor.Domain.Entities;

public sealed class MeterReading : BaseEntity
{
    public required Guid DeduplicationKey { get; init; }

    public required MeterType Type { get; init; }

    public required string Location { get; init; }

    public required DateTimeOffset ReceivedAt { get; init; }

    public required ReadingPayload Payload { get; init; }
}
