using NotificationService.Api.Hubs.Readings.Messages;

namespace NotificationService.Api.Hubs.Readings;

/// <summary>
/// Methods the readings hub calls on its clients.
/// </summary>
public interface IReadingsClient
{
    // SignalR passes a trailing CancellationToken to the send instead of writing it to the client.
    public Task ReadingStored(LiveReading reading, CancellationToken ct);
}
