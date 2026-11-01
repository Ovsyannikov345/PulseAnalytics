namespace DataProcessor.Domain.Entities.Payloads;

public sealed record MotionPayload : ReadingPayload
{
    public required bool MotionDetected { get; init; }
}
