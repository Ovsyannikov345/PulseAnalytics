using NotificationService.Application.LiveReadings.Enums;

namespace NotificationService.Api.Hubs.Readings.Messages;

public sealed record EnergyLiveReading : LiveReading
{
    public override MeterType Type => MeterType.Energy;

    public required double Energy { get; init; }
}
