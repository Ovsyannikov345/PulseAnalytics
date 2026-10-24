namespace DataIngestor.Infrastructure.WeakApp.Contracts;

internal sealed record EnergyMeterReadingResponse : MeterReadingResponse
{
    public required EnergyPayload Payload { get; init; }

    internal sealed record EnergyPayload
    {
        public required double Energy { get; init; }
    }
}
