using NotificationService.Application.LiveReadings.Enums;

namespace NotificationService.Api.Hubs.Readings.Messages;

public sealed record MotionLiveReading : LiveReading
{
    public override MeterType Type => MeterType.Motion;

    public required bool MotionDetected { get; init; }
}
