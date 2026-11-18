using NotificationService.Application.LiveReadings.Enums;

namespace NotificationService.Application.LiveReadings.Models;

public sealed record AirQualityLiveReadingModel : LiveReadingModel
{
    public override MeterType Type => MeterType.AirQuality;

    public required int Co2 { get; init; }

    public required int Pm25 { get; init; }

    public required int Humidity { get; init; }
}
