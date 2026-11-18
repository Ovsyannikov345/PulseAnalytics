using NotificationService.Application.LiveReadings.Enums;

namespace NotificationService.Application.LiveReadings.Models;

/// <summary>
/// A reading the processor has stored, to be pushed to the connected clients.
/// </summary>
public abstract record LiveReadingModel
{
    /// <summary>
    /// Gets the ID of the stored reading (the same ID the GraphQL API returns).
    /// </summary>
    public required Guid Id { get; init; }

    public abstract MeterType Type { get; }

    public required MeterLocation Location { get; init; }

    /// <summary>
    /// Gets the time when the ingestor received the reading from the external API.
    /// </summary>
    public required DateTimeOffset ReceivedAt { get; init; }
}
