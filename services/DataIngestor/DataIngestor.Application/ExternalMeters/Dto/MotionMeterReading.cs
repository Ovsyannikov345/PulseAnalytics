namespace DataIngestor.Application.ExternalMeters.Dto;

public sealed record MotionMeterReading : MeterReading
{
    public required MotionPayload Payload { get; init; }
}

public sealed record MotionPayload
{
    public required bool MotionDetected { get; init; }
}
