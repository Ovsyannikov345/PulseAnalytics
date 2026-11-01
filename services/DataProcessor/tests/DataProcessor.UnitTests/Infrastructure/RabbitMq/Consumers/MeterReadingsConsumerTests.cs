using System.Text;
using Ausiannikau.Utilities.Results;
using DataProcessor.Application.Readings;
using DataProcessor.Application.Readings.Models;
using DataProcessor.Domain.Constants.Enums;
using DataProcessor.Infrastructure.RabbitMq.Connection;
using DataProcessor.Infrastructure.RabbitMq.Consumers;
using DataProcessor.UnitTests.TestDoubles;
using DataProcessor.UnitTests.Utilities;
using FakeItEasy;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Exceptions;
using Shouldly;
using Xunit;

namespace DataProcessor.UnitTests.Infrastructure.RabbitMq.Consumers;

// Covers what the concrete consumers add on top of BaseRabbitMqConsumer: the queue topology of each reading type
// and turning a delivered message into a reading for the processing service.
public sealed class MeterReadingsConsumerTests : IAsyncDisposable
{
    private const string ExchangeName = "pulse.meter-readings";
    private const ulong DeliveryTag = 7;

    private static readonly Guid MessageId = Guid.Parse(RabbitMqDeliveries.MessageId);
    private static readonly DateTimeOffset ReceivedAt = new(2026, 11, 1, 12, 0, 0, TimeSpan.Zero);

    private readonly IRabbitMqConnectionProvider _connectionProvider = A.Fake<IRabbitMqConnectionProvider>();
    private readonly IConnection _connection = A.Fake<IConnection>();
    private readonly IChannel _channel = A.Fake<IChannel>();
    private readonly IServiceScopeFactory _scopeFactory = A.Fake<IServiceScopeFactory>();
    private readonly IServiceScope _scope = A.Fake<IServiceScope>();
    private readonly IReadingsProcessingService _processingService = A.Fake<IReadingsProcessingService>();
    private readonly FakeLogCollector _logCollector = new();
    private BaseRabbitMqConsumer? _sut;

    public MeterReadingsConsumerTests()
    {
        A.CallTo(() => _connectionProvider.GetConnectionAsync(A<CancellationToken>._))
            .Returns(Result<IConnection>.Succeed(_connection));

        A.CallTo(() => _connection.CreateChannelAsync(A<CreateChannelOptions?>._, A<CancellationToken>._))
            .Returns(_channel);

        var serviceProvider = A.Fake<IServiceProvider>();
        A.CallTo(() => serviceProvider.GetService(typeof(IReadingsProcessingService))).Returns(_processingService);
        A.CallTo(() => _scope.ServiceProvider).Returns(serviceProvider);
        A.CallTo(() => _scopeFactory.CreateScope()).Returns(_scope);

        A.CallTo(() => _processingService.ProcessAsync(A<MeterReadingModel>._, A<CancellationToken>._))
            .Returns(Result.Succeed());
    }

    public enum ReadingType
    {
        Motion,
        Energy,
        AirQuality
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
    public async Task ExecuteAsync_Started_ChecksThatMeterReadingsExchangeExists(ReadingType readingType)
    {
        // Act
        await StartConsumingAsync(readingType);

        // Assert
        A.CallTo(() => _channel.ExchangeDeclarePassiveAsync(ExchangeName, A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    [Theory]
    [InlineData(ReadingType.Motion, "pulse.meter-readings.motion.process")]
    [InlineData(ReadingType.Energy, "pulse.meter-readings.energy.process")]
    [InlineData(ReadingType.AirQuality, "pulse.meter-readings.air-quality.process")]
    public async Task ExecuteAsync_Started_DeclaresDurableSharedQueueOfItsReadingType(ReadingType readingType, string queueName)
    {
        // Act
        await StartConsumingAsync(readingType);

        // Assert
        A.CallTo(() => _channel.QueueDeclareAsync(
                queueName,
                true,
                false,
                false,
                A<IDictionary<string, object?>?>._,
                false,
                A<bool>._,
                A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    [Theory]
    [InlineData(ReadingType.Motion, "pulse.meter-readings.motion.process", "meter-reading.motion.received")]
    [InlineData(ReadingType.Energy, "pulse.meter-readings.energy.process", "meter-reading.energy.received")]
    [InlineData(ReadingType.AirQuality, "pulse.meter-readings.air-quality.process", "meter-reading.air-quality.received")]
    public async Task ExecuteAsync_Started_BindsQueueToExchangeWithRoutingKeyOfItsReadingType(
        ReadingType readingType,
        string queueName,
        string routingKey)
    {
        // Act
        await StartConsumingAsync(readingType);

        // Assert
        A.CallTo(() => _channel.QueueBindAsync(
                queueName,
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
        A.CallTo(() => _channel.BasicQosAsync(0, 10, false, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    }

    [Theory]
    [InlineData(ReadingType.Motion, "pulse.meter-readings.motion.process")]
    [InlineData(ReadingType.Energy, "pulse.meter-readings.energy.process")]
    [InlineData(ReadingType.AirQuality, "pulse.meter-readings.air-quality.process")]
    public async Task ExecuteAsync_Started_ConsumesItsQueueOnlyAfterTopologyIsSetUp(ReadingType readingType, string queueName)
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
                .WhenArgumentsMatch(arguments => arguments.Get<string>(0) == queueName)
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
    public async Task MessageDelivered_ValidBody_ProcessesReadingMappedFromIt(ReadingType readingType)
    {
        // Arrange
        var consumer = await StartConsumingAsync(readingType);

        // Act
        await DeliverAsync(consumer, CreateBody(readingType));

        // Assert
        A.CallTo(() => _processingService.ProcessAsync(CreateExpectedModel(readingType), A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    [Theory]
    [InlineData(ReadingType.Motion)]
    [InlineData(ReadingType.Energy)]
    [InlineData(ReadingType.AirQuality)]
    public async Task MessageDelivered_ReadingProcessed_AcksMessage(ReadingType readingType)
    {
        // Arrange
        var consumer = await StartConsumingAsync(readingType);

        // Act
        await DeliverAsync(consumer, CreateBody(readingType));

        // Assert
        A.CallTo(() => _channel.BasicAckAsync(DeliveryTag, false, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
        _channel.CallsToBasicNackAsync().MustNotHaveHappened();
    }

    [Fact]
    public async Task MessageDelivered_ReadingProcessed_DisposesScopeOfMessage()
    {
        // Arrange
        var consumer = await StartConsumingAsync(ReadingType.Energy);

        // Act
        await DeliverAsync(consumer, CreateBody(ReadingType.Energy));

        // Assert
        A.CallTo(() => _scopeFactory.CreateScope()).MustHaveHappenedOnceExactly();
        A.CallTo(() => _scope.Dispose()).MustHaveHappenedOnceExactly();
    }

    [Theory]
    [InlineData(ReadingType.Motion, "not json")]
    [InlineData(ReadingType.Energy, "null")]
    [InlineData(ReadingType.Energy, "{}")]
    [InlineData(ReadingType.AirQuality, """{"messageId":"0199f5c4-3a1e-7b52-9d0e-5c8a7e6b4f21","location":"Kitchen","co2":803}""")]
    [InlineData(
        ReadingType.Motion,
        """{"messageId":"0199f5c4-3a1e-7b52-9d0e-5c8a7e6b4f21","location":"Attic","receivedAt":"2026-11-01T12:00:00+00:00","motionDetected":true}""")]
    public async Task MessageDelivered_InvalidBody_NacksWithoutRequeueAndDoesNotProcessIt(ReadingType readingType, string body)
    {
        // Arrange
        var consumer = await StartConsumingAsync(readingType);

        // Act
        await DeliverAsync(consumer, body);

        // Assert
        A.CallTo(() => _channel.BasicNackAsync(DeliveryTag, false, false, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
        _channel.CallsToBasicAckAsync().MustNotHaveHappened();
        A.CallTo(_processingService).MustNotHaveHappened();
    }

    [Fact]
    public async Task MessageDelivered_ProcessingFails_NacksWithoutRequeue()
    {
        // Arrange
        A.CallTo(() => _processingService.ProcessAsync(A<MeterReadingModel>._, A<CancellationToken>._))
            .Returns(Result.Fail("No processing strategy for EnergyReadingModel"));
        var consumer = await StartConsumingAsync(ReadingType.Energy);

        // Act
        await DeliverAsync(consumer, CreateBody(ReadingType.Energy));

        // Assert
        A.CallTo(() => _channel.BasicNackAsync(DeliveryTag, false, false, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
        _channel.CallsToBasicAckAsync().MustNotHaveHappened();
    }

    // Bodies as the ingestor serializes the contract events: camelCase JSON.
    private static string CreateBody(ReadingType readingType)
    {
        const string commonFields = """
            "messageId":"0199f5c4-3a1e-7b52-9d0e-5c8a7e6b4f21","location":"Kitchen","receivedAt":"2026-11-01T12:00:00+00:00"
            """;

        return readingType switch
        {
            ReadingType.Motion => $$"""{{{commonFields}},"motionDetected":true}""",
            ReadingType.Energy => $$"""{{{commonFields}},"energy":845.62}""",
            _ => $$"""{{{commonFields}},"co2":803,"pm25":25,"humidity":36}"""
        };
    }

    private static MeterReadingModel CreateExpectedModel(ReadingType readingType)
    {
        return readingType switch
        {
            ReadingType.Motion => new MotionReadingModel
            {
                DeduplicationKey = MessageId,
                Location = MeterLocation.Kitchen,
                ReceivedAt = ReceivedAt,
                MotionDetected = true
            },
            ReadingType.Energy => new EnergyReadingModel
            {
                DeduplicationKey = MessageId,
                Location = MeterLocation.Kitchen,
                ReceivedAt = ReceivedAt,
                Energy = 845.62
            },
            _ => new AirQualityReadingModel
            {
                DeduplicationKey = MessageId,
                Location = MeterLocation.Kitchen,
                ReceivedAt = ReceivedAt,
                Co2 = 803,
                Pm25 = 25,
                Humidity = 36
            }
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
                _scopeFactory,
                new FakeLogger<MotionReadingsConsumer>(_logCollector)),
            ReadingType.Energy => new EnergyReadingsConsumer(
                _connectionProvider,
                _scopeFactory,
                new FakeLogger<EnergyReadingsConsumer>(_logCollector)),
            _ => new AirQualityReadingsConsumer(
                _connectionProvider,
                _scopeFactory,
                new FakeLogger<AirQualityReadingsConsumer>(_logCollector))
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
