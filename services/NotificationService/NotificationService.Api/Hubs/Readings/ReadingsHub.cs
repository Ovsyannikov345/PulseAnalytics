using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.SignalR;

namespace NotificationService.Api.Hubs.Readings;

public sealed class ReadingsHub : Hub<IReadingsClient>
{
    public const string Route = "/hubs/readings";
}

internal static class ReadingsHubJsonOptions
{
    public static void Configure(JsonSerializerOptions options)
    {
        options.Converters.Add(new JsonStringEnumConverter());
    }
}
