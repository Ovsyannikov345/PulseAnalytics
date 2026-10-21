namespace DataIngestor.Application.ExternalMeters.Dto;

public sealed record EnergyMeterReading : MeterReading
{
    public required EnergyPayload Payload { get; init; }
}

public sealed record EnergyPayload
{
    public required double Energy { get; init; }
}
