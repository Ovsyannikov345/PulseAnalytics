namespace DataIngestor.Domain.Entities;

public sealed record EnergyPayload : ReadingPayload
{
    public required double Energy { get; init; }
}
