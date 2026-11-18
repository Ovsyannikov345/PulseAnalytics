using Microsoft.AspNetCore.SignalR;

namespace NotificationService.Api.Hubs.Readings;

/// <summary>
/// Pushes stored readings to every connected client. Server-to-client only: clients connect and listen.
/// </summary>
public sealed class ReadingsHub : Hub<IReadingsClient>
{
    public const string Route = "/hubs/readings";
}
