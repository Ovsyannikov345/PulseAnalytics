namespace DataIngestor.Application.ExternalMeters.Dto;

public sealed record AirQualityMeterReading : MeterReading
{
    public required AirQualityPayload Payload { get; init; }
}

public sealed record AirQualityPayload
{
    public required int Co2 { get; init; }

    public required int Pm25 { get; init; }

    public required int Humidity { get; init; }
}
