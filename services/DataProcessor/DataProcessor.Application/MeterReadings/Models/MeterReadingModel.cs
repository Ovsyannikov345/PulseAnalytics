using DataProcessor.Domain.Constants.Enums;

namespace DataProcessor.Application.MeterReadings.Models;

public abstract record MeterReadingModel
{
    public required Guid DeduplicationKey { get; init; }

    public required MeterLocation Location { get; init; }

    public required DateTimeOffset ReceivedAt { get; init; }
}
