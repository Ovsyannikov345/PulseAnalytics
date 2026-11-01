namespace DataProcessor.Domain.Entities.MeterReadings.Payloads;

public sealed record MotionPayload : ReadingPayload
{
    public required bool MotionDetected { get; init; }
}
