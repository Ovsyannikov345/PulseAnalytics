using System.Text;
using Ausiannikau.Utilities.Results;
using FakeItEasy;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using NotificationService.Application.LiveReadings;
using NotificationService.Application.LiveReadings.Enums;
using NotificationService.Application.LiveReadings.Models;
using NotificationService.Application.Telemetry.Metrics;
using NotificationService.Application.Telemetry.Metrics.Enums;
using NotificationService.Application.Telemetry.Traces;
using NotificationService.Infrastructure.RabbitMq.Connection;
using NotificationService.Infrastructure.RabbitMq.Consumers;
using NotificationService.Infrastructure.RabbitMq.Topology;
using NotificationService.UnitTests.TestDoubles;
using NotificationService.UnitTests.Utilities;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Exceptions;
using Shouldly;
using Xunit;

namespace NotificationService.UnitTests.Infrastructure.RabbitMq.Consumers;

// Covers what the concrete consumers add on top of BaseRabbitMqConsumer: the per-instance queue of each reading type
// and turning a delivered stored event into a reading for the broadcast service.
public sealed class StoredReadingsConsumerTests : IAsyncDisposable
{
    private const string ExchangeName = "pulse.meter-readings.stored";
    private const ulong DeliveryTag = 7;

    private static readonly Guid ReadingId = Guid.Parse("019a91b2-4c6e-7d13-9a58-3e7f2b6c1d01");
    private static readonly DateTimeOffset ReceivedAt = new(2026, 11, 17, 10, 15, 30, TimeSpan.Zero);

    private readonly IRabbitMqConnectionProvider _connectionProvider = A.Fake<IRabbitMqConnectionProvider>();
    private readonly IConnection _connection = A.Fake<IConnection>();
    private readonly IChannel _channel = A.Fake<IChannel>();
    private readonly IReadingsBroadcastService _broadcastService = A.Fake<IReadingsBroadcastService>();
    private readonly ServiceInstance _instance = new() { Id = Guid.Parse("019a91b2-0000-7000-8000-000000000001") };
    private readonly FakeLogCollector _logCollector = new();
    private readonly IMetricsService _metrics = A.Fake<IMetricsService>();
    private readonly ITracingService _tracing = A.Fake<ITracingService>();
    private BaseRabbitMqConsumer? _sut;

    public StoredReadingsConsumerTests()
    {
        A.CallTo(() => _connectionProvider.GetConnectionAsync(A<CancellationToken>._))
            .Returns(Result<IConnection>.Succeed(_connection));

        A.CallTo(() => _connection.CreateChannelAsync(A<CreateChannelOptions?>._, A<CancellationToken>._))
            .Returns(_channel);

        // Spans are covered by BaseRabbitMqConsumerTests; here processing runs as if no listener sampled it.
        A.CallTo(() => _tracing.StartMessageProcessing(A<string>._, A<string?>._, A<Guid>._))
            .Returns(null);
    }

    public enum ReadingType
    {
        Motion,
        Energy,
        AirQuality,
    }

    // Stops the consumer even when a test failed before stopping it, so its background task
    // cannot outlive the test.
    public async ValueTask DisposeAsync()
    {
        if (_sut is not null)
        {
            await _sut.StopAsync(CancellationToken.None);

            _sut.Dispose();
        }
    }

    [Theory]
    [InlineData(ReadingType.Motion)]
    [InlineData(ReadingType.Energy)]
    [InlineData(ReadingType.AirQuality)]
    public async Task ExecuteAsync_Started_ChecksThatStoredReadingsExchangeExists(ReadingType readingType)
    {
        // Act
        await StartConsumingAsync(readingType);

        // Assert
        A.CallTo(() => _channel.ExchangeDeclarePassiveAsync(ExchangeName, A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    [Theory]
    [InlineData(ReadingType.Motion, "pulse.meter-readings.stored.motion.notify")]
    [InlineData(ReadingType.Energy, "pulse.meter-readings.stored.energy.notify")]
    [InlineData(ReadingType.AirQuality, "pulse.meter-readings.stored.air-quality.notify")]
    public async Task ExecuteAsync_Started_DeclaresExclusiveAutoDeleteQueueOfItsReadingTypeForThisInstance(
        ReadingType readingType,
        string queueNamePrefix)
    {
        // Act
        await StartConsumingAsync(readingType);

        // Assert
        A.CallTo(() => _channel.QueueDeclareAsync(
                $"{queueNamePrefix}.{_instance.Id}",
                false,
                true,
                true,
                A<IDictionary<string, object?>?>._,
                false,
                A<bool>._,
                A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    [Theory]
    [InlineData(ReadingType.Motion)]
    [InlineData(ReadingType.Energy)]
    [InlineData(ReadingType.AirQuality)]
    public async Task ExecuteAsync_Started_DeclaresQueueThatDropsMessagesOlderThan30Seconds(ReadingType readingType)
    {
        // Arrange
        IDictionary<string, object?>? arguments = null;
        _channel.CallsToQueueDeclareAsync()
            .Invokes(call => arguments = call.GetArgument<IDictionary<string, object?>?>(4));

        // Act
        await StartConsumingAsync(readingType);

        // Assert
        arguments.ShouldNotBeNull().ShouldBe(new Dictionary<string, object?> { ["x-message-ttl"] = 30_000 });
    }

    [Theory]
    [InlineData(ReadingType.Motion, "pulse.meter-readings.stored.motion.notify", "meter-reading.motion.stored")]
    [InlineData(ReadingType.Energy, "pulse.meter-readings.stored.energy.notify", "meter-reading.energy.stored")]
    [InlineData(ReadingType.AirQuality, "pulse.meter-readings.stored.air-quality.notify", "meter-reading.air-quality.stored")]
    public async Task ExecuteAsync_Started_BindsQueueToExchangeWithRoutingKeyOfItsReadingType(
        ReadingType readingType,
        string queueNamePrefix,
        string routingKey)
    {
        // Act
        await StartConsumingAsync(readingType);

        // Assert
        A.CallTo(() => _channel.QueueBindAsync(
                $"{queueNamePrefix}.{_instance.Id}",
                ExchangeName,
                routingKey,
                A<IDictionary<string, object?>?>._,
                A<bool>._,
                A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    [Theory]
    [InlineData(ReadingType.Motion)]
    [InlineData(ReadingType.Energy)]
    [InlineData(ReadingType.AirQuality)]
    public async Task ExecuteAsync_Started_LimitsUnacknowledgedMessagesPerConsumer(ReadingType readingType)
    {
        // Act
        await StartConsumingAsync(readingType);

        // Assert
        A.CallTo(() => _channel.BasicQosAsync(0, 50, false, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    }

    [Theory]
    [InlineData(ReadingType.Motion, "pulse.meter-readings.stored.motion.notify")]
    [InlineData(ReadingType.Energy, "pulse.meter-readings.stored.energy.notify")]
    [InlineData(ReadingType.AirQuality, "pulse.meter-readings.stored.air-quality.notify")]
    public async Task ExecuteAsync_Started_ConsumesItsQueueOnlyAfterTopologyIsSetUp(ReadingType readingType, string queueNamePrefix)
    {
        // Act
        await StartConsumingAsync(readingType);

        // Assert
        A.CallTo(() => _channel.ExchangeDeclarePassiveAsync(A<string>._, A<CancellationToken>._))
            .MustHaveHappenedOnceExactly()
            .Then(_channel.CallsToQueueDeclareAsync().MustHaveHappenedOnceExactly())
            .Then(A.CallTo(() => _channel.QueueBindAsync(
                    A<string>._,
                    A<string>._,
                    A<string>._,
                    A<IDictionary<string, object?>?>._,
                    A<bool>._,
                    A<CancellationToken>._))
                .MustHaveHappenedOnceExactly())
            .Then(A.CallTo(() => _channel.BasicQosAsync(A<uint>._, A<ushort>._, A<bool>._, A<CancellationToken>._))
                .MustHaveHappenedOnceExactly())
            .Then(_channel.CallsToBasicConsumeAsync()
                .WhenArgumentsMatch(arguments => arguments.Get<string>(0) == $"{queueNamePrefix}.{_instance.Id}")
                .MustHaveHappenedOnceExactly());
    }

    [Theory]
    [InlineData(ReadingType.Motion)]
    [InlineData(ReadingType.Energy)]
    [InlineData(ReadingType.AirQuality)]
    public async Task ExecuteAsync_ExchangeDoesNotExist_LogsFailureWithoutDeclaringQueue(ReadingType readingType)
    {
        // Arrange
        var exception = new OperationInterruptedException(
            new ShutdownEventArgs(ShutdownInitiator.Peer, 404, $"NOT_FOUND - no exchange '{ExchangeName}' in vhost '/'"));
        A.CallTo(() => _channel.ExchangeDeclarePassiveAsync(A<string>._, A<CancellationToken>._)).ThrowsAsync(exception);
        var sut = CreateSut(readingType);

        // Act
        await sut.StartAsync(TestContext.Current.CancellationToken);
        await TestWait.UntilAsync(
            () => _logCollector.GetSnapshot().Any(entry => entry.Level == LogLevel.Error),
            TestContext.Current.CancellationToken);
        await sut.StopAsync(TestContext.Current.CancellationToken);

        // Assert
        var error = _logCollector.GetSnapshot().Where(entry => entry.Level == LogLevel.Error).ShouldHaveSingleItem();
        error.Message.ShouldBe("Failed to start RabbitMQ consumer");
        error.Exception.ShouldBeSameAs(exception);
        _channel.CallsToQueueDeclareAsync().MustNotHaveHappened();
        _channel.CallsToBasicConsumeAsync().MustNotHaveHappened();
    }

    [Theory]
    [InlineData(ReadingType.Motion)]
    [InlineData(ReadingType.Energy)]
    [InlineData(ReadingType.AirQuality)]
    public async Task MessageDelivered_ValidBody_BroadcastsReadingMappedFromIt(ReadingType readingType)
    {
        // Arrange
        var consumer = await StartConsumingAsync(readingType);

        // Act
        await DeliverAsync(consumer, CreateBody(readingType));

        // Assert
        A.CallTo(() => _broadcastService.BroadcastAsync(CreateExpectedModel(readingType), A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    [Theory]
    [InlineData(ReadingType.Motion)]
    [InlineData(ReadingType.Energy)]
    [InlineData(ReadingType.AirQuality)]
    public async Task MessageDelivered_ReadingBroadcast_AcksMessage(ReadingType readingType)
    {
        // Arrange
        var consumer = await StartConsumingAsync(readingType);

        // Act
        await DeliverAsync(consumer, CreateBody(readingType));

        // Assert
        A.CallTo(() => _channel.BasicAckAsync(DeliveryTag, false, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
        _channel.CallsToBasicNackAsync().MustNotHaveHappened();
    }

    [Theory]
    [InlineData(ReadingType.Motion, "not json")]
    [InlineData(ReadingType.Energy, "null")]
    [InlineData(ReadingType.Energy, "{}")]
    [InlineData(
        ReadingType.AirQuality,
        """{"messageId":"019a91b2-4c6e-7d13-9a58-3e7f2b6c1e01","readingId":"019a91b2-4c6e-7d13-9a58-3e7f2b6c1d01","location":"Kitchen","co2":803}""")]
    [InlineData(
        ReadingType.Motion,
        """{"messageId":"019a91b2-4c6e-7d13-9a58-3e7f2b6c1e01","readingId":"019a91b2-4c6e-7d13-9a58-3e7f2b6c1d01","location":"Attic","receivedAt":"2026-11-17T10:15:30+00:00","motionDetected":true}""")]
    [InlineData(
        ReadingType.Energy,
        """{"messageId":"019a91b2-4c6e-7d13-9a58-3e7f2b6c1e01","readingId":"019a91b2-4c6e-7d13-9a58-3e7f2b6c1d01","location":99,"receivedAt":"2026-11-17T10:15:30+00:00","energy":845.62}""")]
    public async Task MessageDelivered_InvalidBody_NacksWithoutRequeueAndDoesNotBroadcastIt(ReadingType readingType, string body)
    {
        // Arrange
        var consumer = await StartConsumingAsync(readingType);

        // Act
        await DeliverAsync(consumer, body);

        // Assert
        A.CallTo(() => _channel.BasicNackAsync(DeliveryTag, false, false, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
        _channel.CallsToBasicAckAsync().MustNotHaveHappened();
        A.CallTo(_broadcastService).MustNotHaveHappened();
    }

    [Fact]
    public async Task MessageDelivered_BroadcastThrows_NacksWithoutRequeue()
    {
        // Arrange
        A.CallTo(() => _broadcastService.BroadcastAsync(A<LiveReadingModel>._, A<CancellationToken>._))
            .ThrowsAsync(new InvalidOperationException("Hub is not available"));
        var consumer = await StartConsumingAsync(ReadingType.Energy);

        // Act
        await DeliverAsync(consumer, CreateBody(ReadingType.Energy));

        // Assert
        A.CallTo(() => _channel.BasicNackAsync(DeliveryTag, false, false, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
        _channel.CallsToBasicAckAsync().MustNotHaveHappened();
    }

    [Theory]
    [InlineData(ReadingType.Motion, "MotionReadingStored")]
    [InlineData(ReadingType.Energy, "EnergyReadingStored")]
    [InlineData(ReadingType.AirQuality, "AirQualityReadingStored")]
    public async Task MessageDelivered_AnyReadingType_TracesAndMeasuresItUnderItsContractEventName(
        ReadingType readingType,
        string expectedMessageType)
    {
        // Arrange
        var consumer = await StartConsumingAsync(readingType);

        // Act
        await DeliverAsync(consumer, CreateBody(readingType));

        // Assert
        A.CallTo(() => _tracing.StartMessageProcessing(expectedMessageType, RabbitMqDeliveries.MessageId, A<Guid>._))
            .MustHaveHappenedOnceExactly();
        A.CallTo(() => _metrics.RecordMessageProcessed(expectedMessageType, A<TimeSpan>._, ConsumeOutcome.Acked))
            .MustHaveHappenedOnceExactly();
    }

    // Bodies as the processor serializes the contract events: camelCase JSON, locations by name.
    private static string CreateBody(ReadingType readingType)
    {
        const string commonFields = """
            "messageId":"019a91b2-4c6e-7d13-9a58-3e7f2b6c1e01","readingId":"019a91b2-4c6e-7d13-9a58-3e7f2b6c1d01","location":"LivingRoom","receivedAt":"2026-11-17T10:15:30+00:00"
            """;

        return readingType switch
        {
            ReadingType.Motion => $$"""{{{commonFields}},"motionDetected":true}""",
            ReadingType.Energy => $$"""{{{commonFields}},"energy":845.62}""",
            _ => $$"""{{{commonFields}},"co2":803,"pm25":25,"humidity":36}""",
        };
    }

    private static LiveReadingModel CreateExpectedModel(ReadingType readingType)
    {
        return readingType switch
        {
            ReadingType.Motion => new MotionLiveReadingModel
            {
                Id = ReadingId,
                Location = MeterLocation.LivingRoom,
                ReceivedAt = ReceivedAt,
                MotionDetected = true,
            },
            ReadingType.Energy => new EnergyLiveReadingModel
            {
                Id = ReadingId,
                Location = MeterLocation.LivingRoom,
                ReceivedAt = ReceivedAt,
                Energy = 845.62,
            },
            _ => new AirQualityLiveReadingModel
            {
                Id = ReadingId,
                Location = MeterLocation.LivingRoom,
                ReceivedAt = ReceivedAt,
                Co2 = 803,
                Pm25 = 25,
                Humidity = 36,
            },
        };
    }

    private static Task DeliverAsync(IAsyncBasicConsumer consumer, string body)
    {
        return consumer.DeliverAsync(
            DeliveryTag,
            RabbitMqDeliveries.Properties(),
            Encoding.UTF8.GetBytes(body),
            TestContext.Current.CancellationToken);
    }

    private BaseRabbitMqConsumer CreateSut(ReadingType readingType)
    {
        _sut = readingType switch
        {
            ReadingType.Motion => new MotionReadingsConsumer(
                _connectionProvider,
                _broadcastService,
                _instance,
                TimeProvider.System,
                _metrics,
                _tracing,
                new FakeLogger<MotionReadingsConsumer>(_logCollector)),
            ReadingType.Energy => new EnergyReadingsConsumer(
                _connectionProvider,
                _broadcastService,
                _instance,
                TimeProvider.System,
                _metrics,
                _tracing,
                new FakeLogger<EnergyReadingsConsumer>(_logCollector)),
            _ => new AirQualityReadingsConsumer(
                _connectionProvider,
                _broadcastService,
                _instance,
                TimeProvider.System,
                _metrics,
                _tracing,
                new FakeLogger<AirQualityReadingsConsumer>(_logCollector)),
        };

        return _sut;
    }

    private async Task<IAsyncBasicConsumer> StartConsumingAsync(ReadingType readingType)
    {
        var sut = CreateSut(readingType);

        await sut.StartAsync(TestContext.Current.CancellationToken);

        await TestWait.UntilAsync(() => _channel.RegisteredConsumers().Count > 0, TestContext.Current.CancellationToken);

        return _channel.RegisteredConsumers().ShouldHaveSingleItem();
    }
}
