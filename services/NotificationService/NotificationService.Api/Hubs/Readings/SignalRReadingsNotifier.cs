using Microsoft.AspNetCore.SignalR;
using NotificationService.Api.Hubs.Readings.Messages;
using NotificationService.Application.LiveReadings;
using NotificationService.Application.LiveReadings.Models;

namespace NotificationService.Api.Hubs.Readings;

internal sealed class SignalRReadingsNotifier(IHubContext<ReadingsHub, IReadingsClient> hubContext) : IReadingsNotifier
{
    public Task NotifyReadingStoredAsync(LiveReadingModel reading, CancellationToken ct)
    {
        return hubContext.Clients.All.ReadingStored(reading.ToMessage(), ct);
    }
}
