namespace DataIngestor.Infrastructure.WeakApp.Contracts;

internal sealed record AirQualityMeterReadingResponse : MeterReadingResponse
{
    public required AirQualityPayload Payload { get; init; }

    internal sealed record AirQualityPayload
    {
        public required int Co2 { get; init; }

        public required int Pm25 { get; init; }

        public required int Humidity { get; init; }
    }
}
