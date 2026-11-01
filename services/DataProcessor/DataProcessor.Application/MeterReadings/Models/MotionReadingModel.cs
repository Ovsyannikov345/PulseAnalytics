namespace DataProcessor.Application.MeterReadings.Models;

public sealed record MotionReadingModel : MeterReadingModel
{
    public required bool MotionDetected { get; init; }
}
