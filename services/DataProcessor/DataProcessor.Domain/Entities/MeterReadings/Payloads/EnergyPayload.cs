namespace DataProcessor.Domain.Entities.MeterReadings.Payloads;

public sealed record EnergyPayload : IReadingPayload
{
    public required double Energy { get; init; }
}
