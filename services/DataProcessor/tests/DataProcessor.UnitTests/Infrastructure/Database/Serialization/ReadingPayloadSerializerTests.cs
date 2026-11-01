using DataProcessor.Domain.Entities.MeterReadings.Payloads;
using DataProcessor.Infrastructure.Database.Serialization;
using Shouldly;
using Xunit;

namespace DataProcessor.UnitTests.Infrastructure.Database.Serialization;

public sealed class ReadingPayloadSerializerTests
{
    private const string MotionJson = """{"$type":"motion","motionDetected":true}""";
    private const string EnergyJson = """{"$type":"energy","energy":845.62}""";
    private const string AirQualityJson = """{"$type":"air_quality","co2":803,"pm25":25,"humidity":36}""";

    public enum PayloadKind
    {
        Motion,
        Energy,
        AirQuality
    }

    [Theory]
    [InlineData(PayloadKind.Motion, MotionJson)]
    [InlineData(PayloadKind.Energy, EnergyJson)]
    [InlineData(PayloadKind.AirQuality, AirQualityJson)]
    public void Serialize_AnyPayload_WritesTypeDiscriminatorAndCamelCaseValues(PayloadKind kind, string expectedJson)
    {
        // Act
        var json = ReadingPayloadSerializer.Serialize(CreatePayload(kind));

        // Assert
        json.ShouldBe(expectedJson);
    }

    [Theory]
    [InlineData(PayloadKind.Motion, MotionJson)]
    [InlineData(PayloadKind.Energy, EnergyJson)]
    [InlineData(PayloadKind.AirQuality, AirQualityJson)]
    public void Deserialize_JsonOfAnyPayload_ReadsPayloadOfDiscriminatedType(PayloadKind kind, string json)
    {
        // Act
        var payload = ReadingPayloadSerializer.Deserialize(json);

        // Assert
        payload.ShouldBe(CreatePayload(kind));
    }

    // jsonb sorts the keys of an object (shorter keys first), so the discriminator can come after the values.
    [Fact]
    public void Deserialize_DiscriminatorAfterValues_ReadsPayloadOfDiscriminatedType()
    {
        // Act
        var payload = ReadingPayloadSerializer.Deserialize("""{"co2": 803, "pm25": 25, "$type": "air_quality", "humidity": 36}""");

        // Assert
        payload.ShouldBe(new AirQualityPayload { Co2 = 803, Pm25 = 25, Humidity = 36 });
    }

    [Theory]
    [InlineData("""{"$type":"unknown","energy":845.62}""")]
    [InlineData("""{"energy":845.62}""")]
    [InlineData("""{"$type":"energy"}""")]
    [InlineData("null")]
    public void Deserialize_InvalidPayloadJson_Throws(string json)
    {
        // Act & Assert
        Should.Throw<Exception>(() => ReadingPayloadSerializer.Deserialize(json));
    }

    private static IReadingPayload CreatePayload(PayloadKind kind)
    {
        return kind switch
        {
            PayloadKind.Motion => new MotionPayload { MotionDetected = true },
            PayloadKind.Energy => new EnergyPayload { Energy = 845.62 },
            _ => new AirQualityPayload { Co2 = 803, Pm25 = 25, Humidity = 36 }
        };
    }
}
