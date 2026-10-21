namespace DataIngestor.Infrastructure.WeakApp.Contracts;

internal sealed record MotionMeterReadingResponse : MeterReadingResponse
{
    public required MotionPayload Payload { get; init; }

    internal sealed record MotionPayload
    {
        public required bool MotionDetected { get; init; }
    }
}
