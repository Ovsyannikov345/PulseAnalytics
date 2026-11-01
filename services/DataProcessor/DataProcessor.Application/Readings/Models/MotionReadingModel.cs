namespace DataProcessor.Application.Readings.Models;

public sealed record MotionReadingModel : MeterReadingModel
{
    public required bool MotionDetected { get; init; }
}
