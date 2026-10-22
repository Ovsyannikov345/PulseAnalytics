namespace DataIngestor.Domain.Entities;

public sealed record MotionPayload : ReadingPayload
{
    public required bool MotionDetected { get; init; }
}
