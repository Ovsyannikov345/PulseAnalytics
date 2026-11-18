using NotificationService.Application.LiveReadings.Enums;

namespace NotificationService.Api.Hubs.Readings.Messages;

/// <summary>
/// A stored reading as clients receive it. <see cref="Type"/> tells which concrete reading it is.
/// </summary>
public abstract record LiveReading
{
    public abstract MeterType Type { get; }

    public required Guid Id { get; init; }

    public required MeterLocation Location { get; init; }

    public required DateTimeOffset ReceivedAt { get; init; }
}
