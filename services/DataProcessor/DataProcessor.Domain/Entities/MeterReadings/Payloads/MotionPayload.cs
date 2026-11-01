namespace DataProcessor.Domain.Entities.MeterReadings.Payloads;

public sealed record MotionPayload : IReadingPayload
{
    public required bool MotionDetected { get; init; }
}
