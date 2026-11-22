using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using NotificationService.IntegrationTests.Fixtures;
using NotificationService.IntegrationTests.Utilities;
using Shouldly;
using Xunit;

namespace NotificationService.IntegrationTests.Api;

/// <summary>
/// Runs the whole notification service host: a stored event on RabbitMQ -> per-instance queue -> SignalR client.
/// </summary>
public sealed class ReadingsHubTests(RabbitMqContainerFixture rabbitMq)
{
    private const string ReceivedAt = "2026-11-17T10:15:30.123+00:00";

    // The consumers retry every 5 seconds while the exchange is missing.
    private static readonly TimeSpan ConsumerRestartTimeout = TimeSpan.FromSeconds(20);

    [Fact]
    public async Task Host_StoredEventOfEachType_PushesItsReadingToConnectedClient()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var motion = StoredReadingEvents.Motion();
        var energy = StoredReadingEvents.Energy();
        var airQuality = StoredReadingEvents.AirQuality();
        await using var exchange = await StoredReadingsExchange.CreateAsync(rabbitMq, ct);
        await using var factory = new NotificationServiceApplicationFactory(rabbitMq);
        using var queues = new NotificationQueues(rabbitMq);
        await using var client = await ReadingsHubClient.ConnectAsync(factory.Server, ct);
        await queues.WaitUntilBoundAsync(ct);

        // Act
        await exchange.PublishAsync(motion, ct);
        await exchange.PublishAsync(energy, ct);
        await exchange.PublishAsync(airQuality, ct);
        var readings = await client.WaitForReadingsAsync(3, ct);

        // Assert
        ShouldMatch(
            readings,
            [
                $$"""{"type":"Motion","id":"{{motion.ReadingId}}","location":"Garage","receivedAt":"{{ReceivedAt}}","motionDetected":true}""",
                $$"""{"type":"Energy","id":"{{energy.ReadingId}}","location":"Office","receivedAt":"{{ReceivedAt}}","energy":845.62}""",
                $$"""{"type":"AirQuality","id":"{{airQuality.ReadingId}}","location":"LivingRoom","receivedAt":"{{ReceivedAt}}","co2":803,"pm25":25,"humidity":36}""",
            ]);
    }

    [Fact]
    public async Task Host_TwoClientsConnected_PushesReadingToBoth()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var energy = StoredReadingEvents.Energy();
        await using var exchange = await StoredReadingsExchange.CreateAsync(rabbitMq, ct);
        await using var factory = new NotificationServiceApplicationFactory(rabbitMq);
        using var queues = new NotificationQueues(rabbitMq);
        await using var firstClient = await ReadingsHubClient.ConnectAsync(factory.Server, ct);
        await using var secondClient = await ReadingsHubClient.ConnectAsync(factory.Server, ct);
        await queues.WaitUntilBoundAsync(ct);

        // Act
        await exchange.PublishAsync(energy, ct);
        var firstReadings = await firstClient.WaitForReadingsAsync(1, ct);
        var secondReadings = await secondClient.WaitForReadingsAsync(1, ct);

        // Assert
        firstReadings.ShouldHaveSingleItem().GetProperty("id").GetGuid().ShouldBe(energy.ReadingId);
        secondReadings.ShouldHaveSingleItem().GetProperty("id").GetGuid().ShouldBe(energy.ReadingId);
    }

    [Fact]
    public async Task Host_InvalidMessage_RejectsItWithoutPushAndKeepsConsuming()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var motion = StoredReadingEvents.Motion();
        await using var exchange = await StoredReadingsExchange.CreateAsync(rabbitMq, ct);
        await using var factory = new NotificationServiceApplicationFactory(rabbitMq);
        using var queues = new NotificationQueues(rabbitMq);
        await using var client = await ReadingsHubClient.ConnectAsync(factory.Server, ct);
        await queues.WaitUntilBoundAsync(ct);

        // Act
        var invalidMessageId = await exchange.PublishRawAsync(
            StoredReadingsExchange.MotionRoutingKey,
            Encoding.UTF8.GetBytes("""{"readingId":"not a guid"}"""),
            ct);
        await exchange.PublishAsync(motion, ct);

        // Assert
        // Messages of one queue are handled in order, so the invalid one was handled before the valid one was pushed.
        var readings = await client.WaitForReadingsAsync(1, ct);
        readings.ShouldHaveSingleItem().GetProperty("id").GetGuid().ShouldBe(motion.ReadingId);
        factory.Services.GetFakeLogCollector().GetSnapshot().ShouldContain(entry =>
            entry.Level == LogLevel.Warning
            && entry.Message.Contains(invalidMessageId.ToString(), StringComparison.Ordinal)
            && entry.Message.Contains("processing failed", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Host_Running_DeclaresExclusiveAutoDeleteQueuesWithMessageTtl()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        await StoredReadingsExchange.DeclareAsync(rabbitMq, ct);
        await using var factory = new NotificationServiceApplicationFactory(rabbitMq);
        using var queues = new NotificationQueues(rabbitMq);
        factory.StartServer();

        // Act
        await queues.WaitUntilBoundAsync(ct);
        var declared = await queues.GetAsync(ct);

        // Assert
        declared.Count.ShouldBe(NotificationQueues.QueuesPerInstance);
        declared.ShouldAllBe(queue => !queue.Durable && queue.Exclusive && queue.AutoDelete);
        declared.ShouldAllBe(queue => queue.Arguments["x-message-ttl"].GetInt32() == 30_000);
        declared.Select(queue => queue.Name[..queue.Name.IndexOf(".notify.", StringComparison.Ordinal)]).ShouldBe(
            ["pulse.meter-readings.stored.air-quality", "pulse.meter-readings.stored.energy", "pulse.meter-readings.stored.motion"],
            ignoreOrder: true);
        declared.Select(queue => queue.Name[(queue.Name.LastIndexOf('.') + 1)..]).Distinct().ShouldHaveSingleItem();
    }

    [Fact]
    public async Task Host_Stopped_DeletesItsQueues()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        await StoredReadingsExchange.DeclareAsync(rabbitMq, ct);
        await using var factory = new NotificationServiceApplicationFactory(rabbitMq);
        using var queues = new NotificationQueues(rabbitMq);
        factory.StartServer();
        await queues.WaitUntilBoundAsync(ct);

        // Act
        await factory.DisposeAsync();

        // Assert
        await Should.NotThrowAsync(() => queues.WaitUntilDeletedAsync(ct));
    }

    [Fact]
    public async Task Host_ExchangeMissingAtStart_StartsConsumingOnceItIsDeclared()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var airQuality = StoredReadingEvents.AirQuality();
        await StoredReadingsExchange.DeleteAsync(rabbitMq, ct);
        await using var factory = new NotificationServiceApplicationFactory(rabbitMq);
        using var queues = new NotificationQueues(rabbitMq);
        await using var client = await ReadingsHubClient.ConnectAsync(factory.Server, ct);
        await TestWait.UntilAsync(
            () => factory.Services.GetFakeLogCollector().GetSnapshot().Any(entry => entry.Level == LogLevel.Error),
            ConsumerRestartTimeout,
            ct);

        // Act
        await using var exchange = await StoredReadingsExchange.CreateAsync(rabbitMq, ct);
        await queues.WaitUntilBoundAsync(ct);
        await exchange.PublishAsync(airQuality, ct);

        // Assert
        var readings = await client.WaitForReadingsAsync(1, ct);
        readings.ShouldHaveSingleItem().GetProperty("id").GetGuid().ShouldBe(airQuality.ReadingId);
    }

    private static void ShouldMatch(IReadOnlyList<JsonElement> readings, IReadOnlyList<string> expectedJson)
    {
        readings.Count.ShouldBe(expectedJson.Count);

        foreach (var expected in expectedJson)
        {
            var expectedNode = JsonNode.Parse(expected);

            readings.ShouldContain(
                reading => JsonNode.DeepEquals(JsonSerializer.SerializeToNode(reading), expectedNode),
                $"No pushed reading equals {expected}. Pushed: {string.Join(", ", readings.Select(reading => reading.GetRawText()))}");
        }
    }
}
