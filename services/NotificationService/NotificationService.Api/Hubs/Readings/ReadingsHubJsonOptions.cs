using System.Text.Json;
using System.Text.Json.Serialization;

namespace NotificationService.Api.Hubs.Readings;

/// <summary>
/// JSON settings of the readings hub on top of the SignalR defaults (camelCase property names).
/// </summary>
internal static class ReadingsHubJsonOptions
{
    /// <summary>
    /// Writes enums by their names (e.g. <c>AirQuality</c>, <c>LivingRoom</c>) instead of their numbers.
    /// </summary>
    public static void Configure(JsonSerializerOptions options)
    {
        options.Converters.Add(new JsonStringEnumConverter());
    }
}
