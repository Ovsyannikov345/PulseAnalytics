using ApiGateway.Common.Constants.Enums;

namespace ApiGateway.BusinessLogic.MeterReadings.Models;

public sealed record AirQualityReadingModel : MeterReadingModel
{
    public override MeterType Type => MeterType.AirQuality;

    public required int Co2 { get; init; }

    public required int Pm25 { get; init; }

    public required int Humidity { get; init; }
}
