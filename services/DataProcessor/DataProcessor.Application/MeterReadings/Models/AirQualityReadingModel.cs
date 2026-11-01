namespace DataProcessor.Application.MeterReadings.Models;

public sealed record AirQualityReadingModel : MeterReadingModel
{
    public required int Co2 { get; init; }

    public required int Pm25 { get; init; }

    public required int Humidity { get; init; }
}
