using NotificationService.Application.LiveReadings.Enums;

namespace NotificationService.Api.Hubs.Readings.Messages;

/// <summary>
/// A stored reading as clients receive it. <see cref="Type"/> tells which concrete reading it is.
/// </summary>
/// <remarks>
/// <see cref="Type"/> is a plain property rather than a <c>[JsonPolymorphic]</c> discriminator:
/// SignalR serializes hub method arguments by their runtime type, which would leave a discriminator out.
/// </remarks>
public abstract record LiveReading
{
    public abstract MeterType Type { get; }

    public required Guid Id { get; init; }

    public required MeterLocation Location { get; init; }

    public required DateTimeOffset ReceivedAt { get; init; }
}
