namespace DataProcessor.Application.Readings.Models;

public abstract record MeterReadingModel
{
    public required Guid DeduplicationKey { get; init; }

    public required string Location { get; init; }

    public required DateTimeOffset ReceivedAt { get; init; }
}
