using NotificationService.Application.LiveReadings.Enums;

namespace NotificationService.Application.LiveReadings.Models;

public sealed record MotionLiveReadingModel : LiveReadingModel
{
    public override MeterType Type => MeterType.Motion;

    public required bool MotionDetected { get; init; }
}
