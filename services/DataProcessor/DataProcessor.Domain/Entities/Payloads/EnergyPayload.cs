namespace DataProcessor.Domain.Entities.Payloads;

public sealed record EnergyPayload : ReadingPayload
{
    public required double Energy { get; init; }
}
