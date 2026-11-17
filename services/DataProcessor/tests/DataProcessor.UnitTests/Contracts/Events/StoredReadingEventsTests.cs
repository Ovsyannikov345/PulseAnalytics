using System.Text.Json;
using DataProcessor.Contracts.Enums;
using DataProcessor.Contracts.Events;
using Shouldly;
using Xunit;

namespace DataProcessor.UnitTests.Contracts.Events;

public sealed class StoredReadingEventsTests
{
    private static readonly Guid MessageId = Guid.Parse("019a8f2e-5c1d-7e4a-9b3f-2d6c8e0a1b47");

    private static readonly Guid ReadingId = Guid.Parse("019a8f2e-5b90-7d21-8c4e-7f1a3b5d9e02");

    private static readonly DateTimeOffset ReceivedAt = new(2026, 11, 17, 10, 15, 30, 123, TimeSpan.Zero);

    [Fact]
    public void Serialize_MotionReadingStored_WritesCamelCaseFieldsAndLocationName()
    {
        // Arrange
        var storedEvent = new MotionReadingStored
        {
            MessageId = MessageId,
            ReadingId = ReadingId,
            Location = MeterLocation.Garage,
            ReceivedAt = ReceivedAt,
            MotionDetected = true
        };

        // Act
        var json = JsonSerializer.Serialize(storedEvent, JsonSerializerOptions.Web);

        // Assert
        json.ShouldBe(
            """{"motionDetected":true,"messageId":"019a8f2e-5c1d-7e4a-9b3f-2d6c8e0a1b47","readingId":"019a8f2e-5b90-7d21-8c4e-7f1a3b5d9e02","location":"Garage","receivedAt":"2026-11-17T10:15:30.123+00:00"}""");
    }

    [Fact]
    public void Serialize_EnergyReadingStored_WritesCamelCaseFieldsAndLocationName()
    {
        // Arrange
        var storedEvent = new EnergyReadingStored
        {
            MessageId = MessageId,
            ReadingId = ReadingId,
            Location = MeterLocation.Office,
            ReceivedAt = ReceivedAt,
            Energy = 845.62
        };

        // Act
        var json = JsonSerializer.Serialize(storedEvent, JsonSerializerOptions.Web);

        // Assert
        json.ShouldBe(
            """{"energy":845.62,"messageId":"019a8f2e-5c1d-7e4a-9b3f-2d6c8e0a1b47","readingId":"019a8f2e-5b90-7d21-8c4e-7f1a3b5d9e02","location":"Office","receivedAt":"2026-11-17T10:15:30.123+00:00"}""");
    }

    [Fact]
    public void Serialize_AirQualityReadingStored_WritesCamelCaseFieldsAndLocationName()
    {
        // Arrange
        var storedEvent = new AirQualityReadingStored
        {
            MessageId = MessageId,
            ReadingId = ReadingId,
            Location = MeterLocation.LivingRoom,
            ReceivedAt = ReceivedAt,
            Co2 = 803,
            Pm25 = 25,
            Humidity = 36
        };

        // Act
        var json = JsonSerializer.Serialize(storedEvent, JsonSerializerOptions.Web);

        // Assert
        json.ShouldBe(
            """{"co2":803,"pm25":25,"humidity":36,"messageId":"019a8f2e-5c1d-7e4a-9b3f-2d6c8e0a1b47","readingId":"019a8f2e-5b90-7d21-8c4e-7f1a3b5d9e02","location":"LivingRoom","receivedAt":"2026-11-17T10:15:30.123+00:00"}""");
    }

    [Theory]
    [InlineData(MeterLocation.Kitchen, "Kitchen")]
    [InlineData(MeterLocation.LivingRoom, "LivingRoom")]
    [InlineData(MeterLocation.Bedroom, "Bedroom")]
    [InlineData(MeterLocation.Garage, "Garage")]
    [InlineData(MeterLocation.Office, "Office")]
    [InlineData(MeterLocation.Corridor, "Corridor")]
    public void Serialize_AnyLocation_WritesItsName(MeterLocation location, string expectedName)
    {
        // Act
        var json = JsonSerializer.Serialize(location, JsonSerializerOptions.Web);

        // Assert
        json.ShouldBe($"\"{expectedName}\"");
    }

    [Fact]
    public void Deserialize_SerializedEvent_ReturnsEqualEvent()
    {
        // Arrange
        var storedEvent = new AirQualityReadingStored
        {
            MessageId = MessageId,
            ReadingId = ReadingId,
            Location = MeterLocation.Corridor,
            ReceivedAt = ReceivedAt,
            Co2 = 1200,
            Pm25 = 40,
            Humidity = 55
        };
        var json = JsonSerializer.SerializeToUtf8Bytes(storedEvent, JsonSerializerOptions.Web);

        // Act
        var deserialized = JsonSerializer.Deserialize<AirQualityReadingStored>(json, JsonSerializerOptions.Web);

        // Assert
        deserialized.ShouldBe(storedEvent);
    }

    [Fact]
    public void Deserialize_UnknownLocationName_Throws()
    {
        // Arrange
        const string json =
            """{"motionDetected":true,"messageId":"019a8f2e-5c1d-7e4a-9b3f-2d6c8e0a1b47","readingId":"019a8f2e-5b90-7d21-8c4e-7f1a3b5d9e02","location":"Attic","receivedAt":"2026-11-17T10:15:30.123+00:00"}""";

        // Act
        var deserialize = () => JsonSerializer.Deserialize<MotionReadingStored>(json, JsonSerializerOptions.Web);

        // Assert
        deserialize.ShouldThrow<JsonException>();
    }
}
