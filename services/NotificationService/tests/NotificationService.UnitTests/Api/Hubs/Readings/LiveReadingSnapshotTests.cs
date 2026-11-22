using System.Buffers;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.SignalR.Protocol;
using Microsoft.Extensions.DependencyInjection;
using NotificationService.Api;
using NotificationService.Api.Hubs.Readings;
using NotificationService.Api.Hubs.Readings.Messages;
using NotificationService.Application.LiveReadings.Enums;
using Shouldly;
using Xunit;

namespace NotificationService.UnitTests.Api.Hubs.Readings;

// Pins the wire format clients parse: each committed example (Snapshots/*.json) is what the hub's JSON protocol
// writes as the argument of ReadingStored. The frontend's parser tests use the same examples.
public sealed class LiveReadingSnapshotTests : IDisposable
{
    private const string JsonProtocolName = "json";

    private const string SnapshotsDirectory = "Api/Hubs/Readings/Snapshots";

    private static readonly DateTimeOffset ReceivedAt = new(2026, 11, 17, 10, 15, 30, 123, TimeSpan.Zero);

    private readonly ServiceProvider _serviceProvider = new ServiceCollection()
        .AddLogging()
        .AddReadingsHub()
        .BuildServiceProvider();

    public enum ReadingType
    {
        Motion,
        Energy,
        AirQuality,
    }

    [Theory]
    [InlineData(ReadingType.Motion, "motion.json")]
    [InlineData(ReadingType.Energy, "energy.json")]
    [InlineData(ReadingType.AirQuality, "air-quality.json")]
    public async Task ReadingStored_WrittenByHubJsonProtocol_MatchesCommittedExample(ReadingType readingType, string snapshotFile)
    {
        // Arrange
        var expected = JsonNode.Parse(
            await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, SnapshotsDirectory, snapshotFile), TestContext.Current.CancellationToken));

        // Act
        var argument = WriteReadingStoredArgument(CreateReading(readingType));

        // Assert
        JsonNode.DeepEquals(argument, expected).ShouldBeTrue($"Hub wrote {argument?.ToJsonString()}");
    }

    public void Dispose()
    {
        _serviceProvider.Dispose();
    }

    private static LiveReading CreateReading(ReadingType readingType)
    {
        return readingType switch
        {
            ReadingType.Motion => new MotionLiveReading
            {
                Id = Guid.Parse("019a91b2-4c6e-7d13-9a58-3e7f2b6c1d01"),
                Location = MeterLocation.Garage,
                ReceivedAt = ReceivedAt,
                MotionDetected = false,
            },
            ReadingType.Energy => new EnergyLiveReading
            {
                Id = Guid.Parse("019a91b2-4c6e-7d13-9a58-3e7f2b6c1d02"),
                Location = MeterLocation.Office,
                ReceivedAt = ReceivedAt,
                Energy = 845.62,
            },
            _ => new AirQualityLiveReading
            {
                Id = Guid.Parse("019a91b2-4c6e-7d13-9a58-3e7f2b6c1d03"),
                Location = MeterLocation.LivingRoom,
                ReceivedAt = ReceivedAt,
                Co2 = 803,
                Pm25 = 25,
                Humidity = 36,
            },
        };
    }

    private JsonNode? WriteReadingStoredArgument(LiveReading reading)
    {
        var protocol = _serviceProvider.GetServices<IHubProtocol>().Single(protocol => protocol.Name == JsonProtocolName);
        var buffer = new ArrayBufferWriter<byte>();

        protocol.WriteMessage(new InvocationMessage(nameof(IReadingsClient.ReadingStored), [reading]), buffer);

        // Each message ends with the record separator (0x1E).
        var frame = JsonNode.Parse(buffer.WrittenSpan[..^1]).ShouldNotBeNull();

        return frame["arguments"].ShouldNotBeNull().AsArray().ShouldHaveSingleItem();
    }
}
