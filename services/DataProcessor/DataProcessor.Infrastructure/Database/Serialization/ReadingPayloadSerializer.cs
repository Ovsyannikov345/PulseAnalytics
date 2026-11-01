using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using DataProcessor.Domain.Entities.MeterReadings.Payloads;

namespace DataProcessor.Infrastructure.Database.Serialization;

internal static class ReadingPayloadSerializer
{
    private const string TypeDiscriminatorPropertyName = "$type";

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        AllowOutOfOrderMetadataProperties = true,
        TypeInfoResolver = new DefaultJsonTypeInfoResolver
        {
            Modifiers = { AddPayloadTypes }
        }
    };

    public static string Serialize(ReadingPayload payload)
    {
        return JsonSerializer.Serialize(payload, SerializerOptions);
    }

    public static ReadingPayload Deserialize(string json)
    {
        return JsonSerializer.Deserialize<ReadingPayload>(json, SerializerOptions)
            ?? throw new JsonException("Reading payload is null");
    }

    private static void AddPayloadTypes(JsonTypeInfo typeInfo)
    {
        if (typeInfo.Type != typeof(ReadingPayload))
        {
            return;
        }

        typeInfo.PolymorphismOptions = new JsonPolymorphismOptions
        {
            TypeDiscriminatorPropertyName = TypeDiscriminatorPropertyName,
            UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FailSerialization,
            DerivedTypes =
            {
                new JsonDerivedType(typeof(MotionPayload), "motion"),
                new JsonDerivedType(typeof(EnergyPayload), "energy"),
                new JsonDerivedType(typeof(AirQualityPayload), "air_quality")
            }
        };
    }
}
