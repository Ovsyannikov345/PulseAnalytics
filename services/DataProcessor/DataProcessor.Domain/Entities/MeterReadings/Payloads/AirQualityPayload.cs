namespace DataProcessor.Domain.Entities.MeterReadings.Payloads;

public sealed record AirQualityPayload : ReadingPayload
{
    public required int Co2 { get; init; }

    public required int Pm25 { get; init; }

    public required int Humidity { get; init; }
}
