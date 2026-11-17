using DataProcessor.Contracts.Enums;

namespace DataProcessor.Contracts.Events;

/// <summary>
/// Base type with the fields shared by every meter reading stored event.
/// </summary>
public abstract record MeterReadingStoredEvent
{
    /// <summary>
    /// Gets the unique identifier of the message.
    /// </summary>
    public required Guid MessageId { get; init; }

    /// <summary>
    /// Gets the identifier of the stored reading.
    /// </summary>
    public required Guid ReadingId { get; init; }

    /// <summary>
    /// Gets the location of the meter.
    /// </summary>
    public required MeterLocation Location { get; init; }

    /// <summary>
    /// Gets the time when the ingestor received the reading from the external API.
    /// </summary>
    public required DateTimeOffset ReceivedAt { get; init; }
}
