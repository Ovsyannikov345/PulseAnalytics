namespace DataIngestor.Contracts.Events;

/// <summary>
/// Base type with the fields shared by every meter reading event.
/// </summary>
public abstract record MeterReadingEvent
{
    /// <summary>
    /// Gets the unique identifier of the message.
    /// </summary>
    public required Guid MessageId { get; init; }

    /// <summary>
    /// Gets the location of the meter, e.g. <c>Kitchen</c>.
    /// </summary>
    public required string Location { get; init; }

    /// <summary>
    /// Gets the time when the ingestor received the reading from the external API.
    /// </summary>
    public required DateTimeOffset ReceivedAt { get; init; }
}
