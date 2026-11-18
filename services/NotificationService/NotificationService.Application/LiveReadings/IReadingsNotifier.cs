using NotificationService.Application.LiveReadings.Models;

namespace NotificationService.Application.LiveReadings;

/// <summary>
/// Pushes stored readings to the connected clients.
/// </summary>
public interface IReadingsNotifier
{
    public Task NotifyReadingStoredAsync(LiveReadingModel reading, CancellationToken ct);
}
