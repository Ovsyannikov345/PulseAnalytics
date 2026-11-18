using NotificationService.Application.LiveReadings.Enums;

namespace NotificationService.Api.Hubs.Readings.Messages;

public sealed record AirQualityLiveReading : LiveReading
{
    public override MeterType Type => MeterType.AirQuality;

    public required int Co2 { get; init; }

    public required int Pm25 { get; init; }

    public required int Humidity { get; init; }
}
