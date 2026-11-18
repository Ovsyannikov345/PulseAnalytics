using NotificationService.Application.LiveReadings.Enums;

namespace NotificationService.Application.LiveReadings.Models;

public sealed record EnergyLiveReadingModel : LiveReadingModel
{
    public override MeterType Type => MeterType.Energy;

    public required double Energy { get; init; }
}
