using DataProcessor.Domain.Entities.MeterReadings.Payloads;
using DataProcessor.Infrastructure.Database.Serialization;
using Shouldly;
using Xunit;

namespace DataProcessor.UnitTests.Infrastructure.Database.Serialization;

public sealed class ReadingPayloadSerializerTests
{
    public static TheoryData<ReadingPayload, string> PayloadsWithJson => new()
    {
        { new MotionPayload { MotionDetected = true }, """{"$type":"motion","motionDetected":true}""" },
        { new EnergyPayload { Energy = 845.62 }, """{"$type":"energy","energy":845.62}""" },
        { new AirQualityPayload { Co2 = 803, Pm25 = 25, Humidity = 36 }, """{"$type":"air_quality","co2":803,"pm25":25,"humidity":36}""" }
    };

    [Theory]
    [MemberData(nameof(PayloadsWithJson))]
    public void Serialize_AnyPayload_WritesTypeDiscriminatorAndCamelCaseValues(ReadingPayload payload, string expectedJson)
    {
        // Act
        var json = ReadingPayloadSerializer.Serialize(payload);

        // Assert
        json.ShouldBe(expectedJson);
    }

    [Theory]
    [MemberData(nameof(PayloadsWithJson))]
    public void Deserialize_JsonOfAnyPayload_ReadsPayloadOfDiscriminatedType(ReadingPayload expectedPayload, string json)
    {
        // Act
        var payload = ReadingPayloadSerializer.Deserialize(json);

        // Assert
        payload.ShouldBe(expectedPayload);
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
}
