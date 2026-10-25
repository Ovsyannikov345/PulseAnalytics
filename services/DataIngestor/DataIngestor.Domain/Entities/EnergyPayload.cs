namespace DataIngestor.Domain.Entities;

public sealed record EnergyPayload : IReadingPayload
{
    public required double Energy { get; init; }
}
