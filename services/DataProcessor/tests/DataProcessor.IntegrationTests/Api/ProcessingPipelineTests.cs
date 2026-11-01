using System.Text;
using DataProcessor.Domain.Constants.Enums;
using DataProcessor.Domain.Entities.MeterReadings.Payloads;
using DataProcessor.IntegrationTests.Fixtures;
using DataProcessor.IntegrationTests.Utilities;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace DataProcessor.IntegrationTests.Api;

/// <summary>
/// Runs the whole processor host with its consumers: RabbitMQ -> processor -> PostgreSQL.
/// </summary>
public sealed class ProcessingPipelineTests(RabbitMqContainerFixture rabbitMq, PostgreSqlContainerFixture postgreSql)
{
    private static readonly TimeSpan ConsumersFailureTimeout = TimeSpan.FromSeconds(30);

    [Fact]
    public async Task Host_ReadingsOfAllTypesPublished_StoresEachReadingWithItsPayload()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var motion = MeterReadingEvents.Motion();
        var energy = MeterReadingEvents.Energy();
        var airQuality = MeterReadingEvents.AirQuality();
        await using var exchange = await MeterReadingsExchange.CreateAsync(rabbitMq, ct);
        await using var factory = new ProcessorApplicationFactory(rabbitMq, postgreSql);
        factory.StartServer();
        await exchange.WaitForConsumersAsync(ct);

        // Act
        await exchange.PublishAsync(motion, ct);
        await exchange.PublishAsync(energy, ct);
        await exchange.PublishAsync(airQuality, ct);
        var readings = await StoredReadings.WaitForAsync(factory, [motion.MessageId, energy.MessageId, airQuality.MessageId], ct);

        // Assert
        readings.Select(reading => (reading.DeduplicationKey, reading.Type, reading.Location, reading.ReceivedAt, reading.Payload)).ShouldBe(
            [
                (motion.MessageId, MeterType.Motion, MeterLocation.Garage, MeterReadingEvents.ReceivedAt,
                    new MotionPayload { MotionDetected = true }),
                (energy.MessageId, MeterType.Energy, MeterLocation.Office, MeterReadingEvents.ReceivedAt,
                    new EnergyPayload { Energy = 845.62 }),
                (airQuality.MessageId, MeterType.AirQuality, MeterLocation.LivingRoom, MeterReadingEvents.ReceivedAt,
                    new AirQualityPayload { Co2 = 803, Pm25 = 25, Humidity = 36 })
            ],
            ignoreOrder: true);
    }

    [Fact]
    public async Task Host_SameMessageDeliveredTwice_StoresReadingOnce()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var reading = MeterReadingEvents.Motion();
        var marker = MeterReadingEvents.Motion();
        await using var exchange = await MeterReadingsExchange.CreateAsync(rabbitMq, ct);
        await using var factory = new ProcessorApplicationFactory(rabbitMq, postgreSql);
        factory.StartServer();
        await exchange.WaitForConsumersAsync(ct);

        // Act
        await exchange.PublishAsync(reading, ct);
        await exchange.PublishAsync(reading, ct);
        await exchange.PublishAsync(marker, ct);

        // A queue's messages are processed one by one, so both copies are handled once the marker is stored.
        await StoredReadings.WaitForAsync(factory, [marker.MessageId], ct);

        // Assert
        (await StoredReadings.GetAsync(factory, [reading.MessageId], ct)).ShouldHaveSingleItem();
    }

    [Theory]
    [InlineData("{not json")]
    [InlineData("null")]
    [InlineData("""{"location":"Garage","receivedAt":"2026-11-01T12:00:00+00:00","motionDetected":true}""")]
    [InlineData("""{"messageId":"0199f5c4-3a1e-7b52-9d0e-5c8a7e6b4f21","location":"Attic","receivedAt":"2026-11-01T12:00:00+00:00","motionDetected":true}""")]
    public async Task Host_InvalidMessage_DropsItAndKeepsConsuming(string body)
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var validReading = MeterReadingEvents.Motion();
        await using var exchange = await MeterReadingsExchange.CreateAsync(rabbitMq, ct);
        await using var factory = new ProcessorApplicationFactory(rabbitMq, postgreSql);
        factory.StartServer();
        await exchange.WaitForConsumersAsync(ct);
        var storedBefore = await StoredReadings.CountAsync(factory, ct);

        // Act
        await exchange.PublishRawAsync(MeterReadingsExchange.MotionRoutingKey, Encoding.UTF8.GetBytes(body), ct);
        await exchange.PublishAsync(validReading, ct);
        await StoredReadings.WaitForAsync(factory, [validReading.MessageId], ct);

        // Assert
        (await StoredReadings.CountAsync(factory, ct)).ShouldBe(storedBefore + 1);

        // Stopping the processor returns unacked messages to the queue, so an empty queue means the message was not requeued.
        await factory.DisposeAsync();
        (await exchange.GetReadyMessageCountAsync(MeterReadingsExchange.MotionQueue, ct)).ShouldBe(0u);
    }

    [Fact]
    public async Task Host_ExchangeMissingAtStartup_StartsConsumingOnceExchangeIsDeclared()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var reading = MeterReadingEvents.Energy();
        await MeterReadingsExchange.DeleteAsync(rabbitMq, ct);
        await using var factory = new ProcessorApplicationFactory(rabbitMq, postgreSql);
        factory.StartServer();

        // Each consumer fails to set up its queue while the exchange is missing, and retries after a delay.
        var logs = factory.Services.GetFakeLogCollector();
        await TestWait.UntilAsync(
            () => logs.GetSnapshot()
                .Where(record => record.Message.StartsWith("Failed to start RabbitMQ consumer", StringComparison.Ordinal))
                .Select(record => record.Category)
                .Distinct()
                .Count() == MeterReadingsExchange.ProcessingQueues.Count,
            ConsumersFailureTimeout,
            ct);

        // Act
        await using var exchange = await MeterReadingsExchange.CreateAsync(rabbitMq, ct);
        await exchange.WaitForConsumersAsync(ct);
        await exchange.PublishAsync(reading, ct);

        // Assert
        (await StoredReadings.WaitForAsync(factory, [reading.MessageId], ct)).ShouldHaveSingleItem();
    }

    [Fact]
    public async Task Ping_HostIsRunning_ReturnsPong()
    {
        // Arrange
        await using var factory = new ProcessorApplicationFactory(rabbitMq, postgreSql);
        using var client = factory.CreateClient();

        // Act
        var response = await client.GetStringAsync(new Uri("/ping", UriKind.Relative), TestContext.Current.CancellationToken);

        // Assert
        response.ShouldBe("pong");
    }
}
