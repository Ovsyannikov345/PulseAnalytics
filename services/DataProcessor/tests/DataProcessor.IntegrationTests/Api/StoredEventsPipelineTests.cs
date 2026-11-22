using System.Diagnostics;
using System.Diagnostics.Metrics;
using DataProcessor.Contracts.Events;
using DataProcessor.IntegrationTests.Fixtures;
using DataProcessor.IntegrationTests.Utilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.Metrics.Testing;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using Shouldly;
using Xunit;
using ContractMeterLocation = DataProcessor.Contracts.Enums.MeterLocation;

namespace DataProcessor.IntegrationTests.Api;

/// <summary>
/// Runs the whole processor host: RabbitMQ -> processor -> PostgreSQL, then a stored event -> RabbitMQ.
/// </summary>
public sealed class StoredEventsPipelineTests(RabbitMqContainerFixture rabbitMq, PostgreSqlContainerFixture postgreSql)
{
    private const string ProcessorScopeName = "PulseAnalytics.DataProcessor";

    private const string MessagesPublishedName = "data_processor.messages.published";

    private static readonly TimeSpan PublishTimeout = TimeSpan.FromSeconds(30);

    [Fact]
    public async Task Host_ReadingsOfAllTypesStored_PublishesStoredEventForEach()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var motion = MeterReadingEvents.Motion();
        var energy = MeterReadingEvents.Energy();
        var airQuality = MeterReadingEvents.AirQuality();
        await using var storedExchange = await StoredReadingsExchange.CreateAsync(rabbitMq, ct);
        await using var exchange = await MeterReadingsExchange.CreateAsync(rabbitMq, ct);
        await using var factory = new ProcessorApplicationFactory(rabbitMq, postgreSql);
        factory.StartServer();
        await exchange.WaitForConsumersAsync(ct);

        // Act
        await exchange.PublishAsync(motion, ct);
        await exchange.PublishAsync(energy, ct);
        await exchange.PublishAsync(airQuality, ct);
        var readings = await StoredReadings.WaitForAsync(factory, [motion.MessageId, energy.MessageId, airQuality.MessageId], ct);
        var readingIds = readings.ToDictionary(reading => reading.DeduplicationKey, reading => reading.Id);
        var messages = await storedExchange.WaitForReadingsAsync([.. readingIds.Values], ct);

        // Assert
        messages.Select(message => (message.RoutingKey, message.Event with { MessageId = Guid.Empty })).ShouldBe(
            [
                ("meter-reading.motion.stored", new MotionReadingStored
                {
                    MessageId = Guid.Empty,
                    ReadingId = readingIds[motion.MessageId],
                    Location = ContractMeterLocation.Garage,
                    ReceivedAt = MeterReadingEvents.ReceivedAt,
                    MotionDetected = true,
                }),
                ("meter-reading.energy.stored", new EnergyReadingStored
                {
                    MessageId = Guid.Empty,
                    ReadingId = readingIds[energy.MessageId],
                    Location = ContractMeterLocation.Office,
                    ReceivedAt = MeterReadingEvents.ReceivedAt,
                    Energy = 845.62,
                }),
                ("meter-reading.air-quality.stored", new AirQualityReadingStored
                {
                    MessageId = Guid.Empty,
                    ReadingId = readingIds[airQuality.MessageId],
                    Location = ContractMeterLocation.LivingRoom,
                    ReceivedAt = MeterReadingEvents.ReceivedAt,
                    Co2 = 803,
                    Pm25 = 25,
                    Humidity = 36,
                }),
            ],
            ignoreOrder: true);
    }

    [Fact]
    public async Task Host_ReadingStored_PublishesTransientMessageWithMetadataOfIncomingMessage()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var reading = MeterReadingEvents.Energy();
        var correlationId = Guid.CreateVersion7();
        await using var storedExchange = await StoredReadingsExchange.CreateAsync(rabbitMq, ct);
        await using var exchange = await MeterReadingsExchange.CreateAsync(rabbitMq, ct);
        await using var factory = new ProcessorApplicationFactory(rabbitMq, postgreSql);
        factory.StartServer();
        await exchange.WaitForConsumersAsync(ct);

        // Act
        await exchange.PublishAsync(reading, correlationId, ct);
        var readingId = (await StoredReadings.WaitForAsync(factory, [reading.MessageId], ct)).ShouldHaveSingleItem().Id;
        var message = (await storedExchange.WaitForReadingsAsync([readingId], ct)).ShouldHaveSingleItem();

        // Assert
        message.Properties.DeliveryMode.ShouldBe(DeliveryModes.Transient);
        message.Properties.AppId.ShouldBe("data-processor");
        message.Properties.ContentType.ShouldBe("application/json");
        message.Properties.Type.ShouldBe(nameof(EnergyReadingStored));
        message.Properties.MessageId.ShouldBe(message.Event.MessageId.ToString());
        message.Properties.CorrelationId.ShouldBe(correlationId.ToString());
    }

    [Fact]
    public async Task Host_ReadingPublishedWithinTrace_PublishesStoredEventInSameTrace()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var reading = MeterReadingEvents.Motion();
        using var testSource = new ActivitySource(nameof(StoredEventsPipelineTests));
        using var listener = ListenTo(testSource.Name, "RabbitMQ.Client.Publisher", "RabbitMQ.Client.Subscriber", ProcessorScopeName);
        await using var storedExchange = await StoredReadingsExchange.CreateAsync(rabbitMq, ct);
        await using var exchange = await MeterReadingsExchange.CreateAsync(rabbitMq, ct);
        await using var factory = new ProcessorApplicationFactory(rabbitMq, postgreSql);
        factory.StartServer();
        await exchange.WaitForConsumersAsync(ct);

        // Act
        ActivityTraceId traceId;

        // Stands in for the ingestor's poll span: the publish below writes its trace context into the message headers.
        using (var pollActivity = testSource.StartActivity("poll meters").ShouldNotBeNull())
        {
            traceId = pollActivity.TraceId;

            await exchange.PublishAsync(reading, ct);
        }

        var readingId = (await StoredReadings.WaitForAsync(factory, [reading.MessageId], ct)).ShouldHaveSingleItem().Id;
        var message = (await storedExchange.WaitForReadingsAsync([readingId], ct)).ShouldHaveSingleItem();

        // Assert
        var traceParent = message.TraceParent.ShouldNotBeNull();
        ActivityContext.Parse(traceParent, traceState: null).TraceId.ShouldBe(traceId);
    }

    [Fact]
    public async Task Host_SameMessageDeliveredTwice_PublishesOneStoredEvent()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var reading = MeterReadingEvents.Motion();
        var marker = MeterReadingEvents.Motion();
        await using var storedExchange = await StoredReadingsExchange.CreateAsync(rabbitMq, ct);
        await using var exchange = await MeterReadingsExchange.CreateAsync(rabbitMq, ct);
        await using var factory = new ProcessorApplicationFactory(rabbitMq, postgreSql);
        factory.StartServer();
        await exchange.WaitForConsumersAsync(ct);

        // Act
        await exchange.PublishAsync(reading, ct);
        await exchange.PublishAsync(reading, ct);
        await exchange.PublishAsync(marker, ct);

        // A queue's messages are processed one by one, so both copies are handled once the marker's event is published.
        var readings = await StoredReadings.WaitForAsync(factory, [reading.MessageId, marker.MessageId], ct);
        var readingId = readings.Single(stored => stored.DeduplicationKey == reading.MessageId).Id;
        var markerId = readings.Single(stored => stored.DeduplicationKey == marker.MessageId).Id;
        var messages = await storedExchange.WaitForReadingsAsync([readingId, markerId], ct);

        // Assert
        messages.Count(message => message.Event.ReadingId == readingId).ShouldBe(1);
    }

    [Fact]
    public async Task Host_NoQueueBoundToStoredExchange_StoresReadingAndPublishesWithoutWarnings()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var reading = MeterReadingEvents.AirQuality();
        await StoredReadingsExchange.DeleteAsync(rabbitMq, ct);
        await using var exchange = await MeterReadingsExchange.CreateAsync(rabbitMq, ct);
        await using var factory = new ProcessorApplicationFactory(rabbitMq, postgreSql);
        using var publishes = new MetricCollector<long>(
            factory.Services.GetRequiredService<IMeterFactory>(),
            ProcessorScopeName,
            MessagesPublishedName);
        factory.StartServer();
        await exchange.WaitForConsumersAsync(ct);

        // Act
        await exchange.PublishAsync(reading, ct);
        await TestWait.UntilAsync(
            () => publishes.GetMeasurementSnapshot().Any(publish => Equals(publish.Tags["app.message.type"], nameof(AirQualityReadingStored))),
            PublishTimeout,
            ct);

        // Assert
        (await StoredReadings.GetAsync(factory, [reading.MessageId], ct)).ShouldHaveSingleItem();

        // The event is not mandatory: the broker confirms it and drops it, instead of returning it as unroutable.
        publishes.GetMeasurementSnapshot().ShouldAllBe(publish => Equals(publish.Tags["app.outcome"], "confirmed"));
        factory.Services.GetFakeLogCollector().GetSnapshot().ShouldNotContain(record => record.Level >= LogLevel.Warning);
    }

    private static ActivityListener ListenTo(params string[] sourceNames)
    {
        var listener = new ActivityListener
        {
            ShouldListenTo = source => sourceNames.Contains(source.Name),
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
        };

        ActivitySource.AddActivityListener(listener);

        return listener;
    }
}
