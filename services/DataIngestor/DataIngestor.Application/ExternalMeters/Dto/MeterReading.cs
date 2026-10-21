namespace DataIngestor.Application.ExternalMeters.Dto;

public abstract record MeterReading
{
    public required string Name { get; init; }
}
