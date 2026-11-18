using NotificationService.Api.Hubs.Readings.Messages;

namespace NotificationService.Api.Hubs.Readings;

/// <summary>
/// Methods the readings hub calls on its clients.
/// </summary>
public interface IReadingsClient
{
    public Task ReadingStored(LiveReading reading);
}
