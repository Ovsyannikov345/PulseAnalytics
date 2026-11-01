namespace DataProcessor.Domain.Entities.MeterReadings.Payloads;

public sealed record EnergyPayload : ReadingPayload
{
    public required double Energy { get; init; }
}
