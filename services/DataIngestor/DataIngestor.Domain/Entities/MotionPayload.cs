namespace DataIngestor.Domain.Entities;

public sealed record MotionPayload : IReadingPayload
{
    public required bool MotionDetected { get; init; }
}
