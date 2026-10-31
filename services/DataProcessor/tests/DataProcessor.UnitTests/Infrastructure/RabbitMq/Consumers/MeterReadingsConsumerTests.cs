using Ausiannikau.Utilities.Results;
using DataProcessor.Infrastructure.RabbitMq.Connection;
using DataProcessor.Infrastructure.RabbitMq.Consumers;
using DataProcessor.UnitTests.TestDoubles;
using DataProcessor.UnitTests.Utilities;
using FakeItEasy;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Exceptions;
using Shouldly;
using Xunit;

namespace DataProcessor.UnitTests.Infrastructure.RabbitMq.Consumers;

// Covers what the concrete consumers add on top of BaseRabbitMqConsumer: the queue topology of each reading type.
public sealed class MeterReadingsConsumerTests : IAsyncDisposable
{
    private const string ExchangeName = "pulse.meter-readings";

    private readonly IRabbitMqConnectionProvider _connectionProvider = A.Fake<IRabbitMqConnectionProvider>();
    private readonly IConnection _connection = A.Fake<IConnection>();
    private readonly IChannel _channel = A.Fake<IChannel>();
    private readonly FakeLogCollector _logCollector = new();
    private BaseRabbitMqConsumer? _sut;

    public MeterReadingsConsumerTests()
    {
        A.CallTo(() => _connectionProvider.GetConnectionAsync(A<CancellationToken>._))
            .Returns(Result<IConnection>.Succeed(_connection));

        A.CallTo(() => _connection.CreateChannelAsync(A<CreateChannelOptions?>._, A<CancellationToken>._))
            .Returns(_channel);
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
    public async Task MessageDelivered_AnyReadingType_AcksMessage(ReadingType readingType)
    {
        // Arrange
        var consumer = await StartConsumingAsync(readingType);

        // Act
        await consumer.DeliverAsync(7, RabbitMqDeliveries.Properties(), TestContext.Current.CancellationToken);

        // Assert
        A.CallTo(() => _channel.BasicAckAsync(7, false, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
        _channel.CallsToBasicNackAsync().MustNotHaveHappened();
    }

    private BaseRabbitMqConsumer CreateSut(ReadingType readingType)
    {
        _sut = readingType switch
        {
            ReadingType.Motion => new MotionReadingsConsumer(
                _connectionProvider,
                new FakeLogger<MotionReadingsConsumer>(_logCollector)),
            ReadingType.Energy => new EnergyReadingsConsumer(
                _connectionProvider,
                new FakeLogger<EnergyReadingsConsumer>(_logCollector)),
            _ => new AirQualityReadingsConsumer(
                _connectionProvider,
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
