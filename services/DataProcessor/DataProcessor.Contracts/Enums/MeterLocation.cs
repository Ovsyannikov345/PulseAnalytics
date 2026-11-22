using System.Text.Json.Serialization;

namespace DataProcessor.Contracts.Enums;

/// <summary>
/// Location of a meter. Serialized to JSON by name, e.g. <c>LivingRoom</c>.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<MeterLocation>))]
public enum MeterLocation
{
    /// <summary>
    /// Kitchen.
    /// </summary>
    Kitchen = 1,

    /// <summary>
    /// Living room.
    /// </summary>
    LivingRoom = 2,

    /// <summary>
    /// Bedroom.
    /// </summary>
    Bedroom = 3,

    /// <summary>
    /// Garage.
    /// </summary>
    Garage = 4,

    /// <summary>
    /// Office.
    /// </summary>
    Office = 5,

    /// <summary>
    /// Corridor.
    /// </summary>
    Corridor = 6,
}
