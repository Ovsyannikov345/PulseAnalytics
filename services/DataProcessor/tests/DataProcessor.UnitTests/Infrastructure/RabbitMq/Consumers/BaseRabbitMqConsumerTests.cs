using Ausiannikau.Utilities.Results;
using DataProcessor.Application.Correlation;
using DataProcessor.Infrastructure.RabbitMq.Connection;
using DataProcessor.Infrastructure.RabbitMq.Consumers;
using DataProcessor.UnitTests.TestDoubles;
using DataProcessor.UnitTests.Utilities;
using FakeItEasy;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Shouldly;
using Xunit;

namespace DataProcessor.UnitTests.Infrastructure.RabbitMq.Consumers;

// The consumer runs on a background task, so tests wait for its observable effects (registered consumers,
// log records) with TestWait. Deliveries are handed to the registered consumer directly by the test.
public sealed class BaseRabbitMqConsumerTests : IAsyncDisposable
{
    private const string QueueName = DelegatingRabbitMqConsumer.TestQueueName;
    private const ulong DeliveryTag = 42;

    private const string ShuttingDownMessage = "RabbitMQ consumer is shutting down";

    private readonly IRabbitMqConnectionProvider _connectionProvider = A.Fake<IRabbitMqConnectionProvider>();
    private readonly IConnection _connection = A.Fake<IConnection>();
    private readonly IChannel _channel = A.Fake<IChannel>();
    private readonly Func<IChannel, CancellationToken, Task<Result>> _setupQueue = A.Fake<Func<IChannel, CancellationToken, Task<Result>>>();
    private readonly Func<ReceivedMessage, CancellationToken, Task<Result>> _processMessage =
        A.Fake<Func<ReceivedMessage, CancellationToken, Task<Result>>>();
    private readonly FakeLogger<DelegatingRabbitMqConsumer> _logger = new();
    private readonly DelegatingRabbitMqConsumer _sut;

    public BaseRabbitMqConsumerTests()
    {
        A.CallTo(() => _connectionProvider.GetConnectionAsync(A<CancellationToken>._))
            .Returns(Result<IConnection>.Succeed(_connection));

        A.CallTo(() => _connection.CreateChannelAsync(A<CreateChannelOptions?>._, A<CancellationToken>._))
            .Returns(_channel);

        A.CallTo(() => _connection.IsOpen).Returns(true);
        A.CallTo(() => _channel.IsOpen).Returns(true);

        A.CallTo(() => _setupQueue(A<IChannel>._, A<CancellationToken>._))
            .Returns(Result.Succeed());

        A.CallTo(() => _processMessage(A<ReceivedMessage>._, A<CancellationToken>._))
            .Returns(Result.Succeed());

        _sut = new DelegatingRabbitMqConsumer(_connectionProvider, _setupQueue, _processMessage, _logger);
    }

    // Stops the consumer even when a test failed before stopping it, so its background task
    // cannot outlive the test.
    public async ValueTask DisposeAsync()
    {
        await _sut.StopAsync(CancellationToken.None);

        _sut.Dispose();
    }

    [Fact]
    public async Task ExecuteAsync_Started_SetsUpQueueOnOpenedChannelBeforeConsumingIt()
    {
        // Act
        await StartConsumingAsync();

        // Assert
        A.CallTo(() => _setupQueue(_channel, A<CancellationToken>._))
            .MustHaveHappenedOnceExactly()
            .Then(_channel.CallsToBasicConsumeAsync()
                .WhenArgumentsMatch(arguments => arguments.Get<string>(0) == QueueName)
                .MustHaveHappenedOnceExactly());
    }

    [Fact]
    public async Task ExecuteAsync_Started_ConsumesWithManualAcknowledgements()
    {
        // Act
        await StartConsumingAsync();

        // Assert
        _channel.CallsToBasicConsumeAsync()
            .WhenArgumentsMatch(arguments => !arguments.Get<bool>(1))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task ExecuteAsync_Started_PassesStoppingTokenToConnectionProvider()
    {
        // Arrange
        var passedToken = CancellationToken.None;
        A.CallTo(() => _connectionProvider.GetConnectionAsync(A<CancellationToken>._))
            .Invokes((CancellationToken token) => passedToken = token)
            .Returns(Result<IConnection>.Succeed(_connection));

        // Act
        await StartConsumingAsync();
        var cancelledBeforeStop = passedToken.IsCancellationRequested;
        await _sut.StopAsync(TestContext.Current.CancellationToken);

        // Assert
        passedToken.CanBeCanceled.ShouldBeTrue();
        cancelledBeforeStop.ShouldBeFalse();
        passedToken.IsCancellationRequested.ShouldBeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_ConnectionUnavailable_LogsReasonWithoutOpeningChannel()
    {
        // Arrange
        A.CallTo(() => _connectionProvider.GetConnectionAsync(A<CancellationToken>._))
            .Returns(Result<IConnection>.Fail("Failed to connect to RabbitMQ"));

        // Act
        await _sut.StartAsync(TestContext.Current.CancellationToken);
        await WaitForLogAsync("Failed to start RabbitMQ consumer. Failed to connect to RabbitMQ");
        await _sut.StopAsync(TestContext.Current.CancellationToken);

        // Assert
        _logger.Collector.GetSnapshot().Where(entry => entry.Level == LogLevel.Error).ShouldHaveSingleItem();
        A.CallTo(() => _connection.CreateChannelAsync(A<CreateChannelOptions?>._, A<CancellationToken>._))
            .MustNotHaveHappened();
    }

    [Fact]
    public async Task ExecuteAsync_StartFailed_DoesNotRetryBeforeRestartDelay()
    {
        // Arrange
        A.CallTo(() => _connectionProvider.GetConnectionAsync(A<CancellationToken>._))
            .Returns(Result<IConnection>.Fail("Failed to connect to RabbitMQ"));

        // Act
        await _sut.StartAsync(TestContext.Current.CancellationToken);
        await WaitForLogAsync("Failed to start RabbitMQ consumer. Failed to connect to RabbitMQ");
        await _sut.StopAsync(TestContext.Current.CancellationToken);

        // Assert
        A.CallTo(() => _connectionProvider.GetConnectionAsync(A<CancellationToken>._)).MustHaveHappenedOnceExactly();
        _sut.ExecuteTask.ShouldNotBeNull().IsCanceled.ShouldBeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_ChannelCannotBeOpened_LogsException()
    {
        // Arrange
        var exception = new InvalidOperationException("Channel limit reached");
        A.CallTo(() => _connection.CreateChannelAsync(A<CreateChannelOptions?>._, A<CancellationToken>._))
            .ThrowsAsync(exception);

        // Act
        await _sut.StartAsync(TestContext.Current.CancellationToken);
        await WaitForLogAsync("Failed to start RabbitMQ consumer");
        await _sut.StopAsync(TestContext.Current.CancellationToken);

        // Assert
        var error = _logger.Collector.GetSnapshot().Where(entry => entry.Level == LogLevel.Error).ShouldHaveSingleItem();
        error.Exception.ShouldBeSameAs(exception);
        A.CallTo(() => _setupQueue(A<IChannel>._, A<CancellationToken>._)).MustNotHaveHappened();
    }

    [Fact]
    public async Task ExecuteAsync_QueueSetupReturnsFailure_LogsReasonAndClosesChannelWithoutConsuming()
    {
        // Arrange
        A.CallTo(() => _setupQueue(A<IChannel>._, A<CancellationToken>._))
            .Returns(Result.Fail("Exchange pulse.meter-readings does not exist"));

        // Act
        await _sut.StartAsync(TestContext.Current.CancellationToken);
        await WaitForLogAsync("Failed to start RabbitMQ consumer. Exchange pulse.meter-readings does not exist");
        await _sut.StopAsync(TestContext.Current.CancellationToken);

        // Assert
        _logger.Collector.GetSnapshot().Where(entry => entry.Level == LogLevel.Error).ShouldHaveSingleItem();
        _channel.CallsToBasicConsumeAsync().MustNotHaveHappened();
        _channel.CallsToCloseAsync()
            .MustHaveHappenedOnceExactly()
            .Then(A.CallTo(() => _channel.DisposeAsync()).MustHaveHappenedOnceExactly());
    }

    [Fact]
    public async Task ExecuteAsync_QueueSetupThrows_LogsExceptionAndClosesChannelWithoutConsuming()
    {
        // Arrange
        var exception = new InvalidOperationException("PRECONDITION_FAILED - inequivalent arg 'durable'");
        A.CallTo(() => _setupQueue(A<IChannel>._, A<CancellationToken>._)).ThrowsAsync(exception);

        // Act
        await _sut.StartAsync(TestContext.Current.CancellationToken);
        await WaitForLogAsync("Failed to start RabbitMQ consumer");
        await _sut.StopAsync(TestContext.Current.CancellationToken);

        // Assert
        var error = _logger.Collector.GetSnapshot().Where(entry => entry.Level == LogLevel.Error).ShouldHaveSingleItem();
        error.Exception.ShouldBeSameAs(exception);
        _channel.CallsToBasicConsumeAsync().MustNotHaveHappened();
        A.CallTo(() => _channel.DisposeAsync()).MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task ExecuteAsync_ConsumingCannotBeStarted_LogsExceptionAndClosesChannel()
    {
        // Arrange
        var exception = new InvalidOperationException("NOT_FOUND - no queue");
        _channel.CallsToBasicConsumeAsync().ThrowsAsync(exception);

        // Act
        await _sut.StartAsync(TestContext.Current.CancellationToken);
        await WaitForLogAsync("Exception occurred while listening to RabbitMQ messages");
        await _sut.StopAsync(TestContext.Current.CancellationToken);

        // Assert
        var error = _logger.Collector.GetSnapshot().Where(entry => entry.Level == LogLevel.Error).ShouldHaveSingleItem();
        error.Exception.ShouldBeSameAs(exception);
        A.CallTo(() => _channel.DisposeAsync()).MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task ExecuteAsync_ChannelShutDownWhileConnectionIsOpen_LogsStopReasonAndClosesChannel()
    {
        // Arrange
        var consumer = await StartConsumingAsync();
        A.CallTo(() => _channel.IsOpen).Returns(false);

        // Act
        await consumer.HandleChannelShutdownAsync(
            _channel,
            new ShutdownEventArgs(ShutdownInitiator.Peer, 406, "PRECONDITION_FAILED - unknown delivery tag 42"));
        var stopMessage = $"RabbitMQ consumer of queue {QueueName} stopped. Channel was closed. Reply code: 406. " +
            "Reply text: PRECONDITION_FAILED - unknown delivery tag 42. Restarting in 5 seconds";
        await WaitForLogAsync(stopMessage);
        await _sut.StopAsync(TestContext.Current.CancellationToken);

        // Assert
        _logger.Collector.GetSnapshot().ShouldContain(entry => entry.Level == LogLevel.Warning && entry.Message == stopMessage);
        A.CallTo(() => _channel.DisposeAsync()).MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task ExecuteAsync_ChannelShutDownWithConnection_LeavesConsumerToAutomaticRecovery()
    {
        // Arrange
        var consumer = await StartConsumingAsync();
        A.CallTo(() => _connection.IsOpen).Returns(false);
        A.CallTo(() => _channel.IsOpen).Returns(false);

        // Act
        await consumer.HandleChannelShutdownAsync(
            _channel,
            new ShutdownEventArgs(ShutdownInitiator.Peer, 320, "CONNECTION_FORCED"));
        await _sut.StopAsync(TestContext.Current.CancellationToken);

        // Assert
        _logger.Collector.GetSnapshot().ShouldNotContain(entry => entry.Level >= LogLevel.Warning);
        _logger.Collector.GetSnapshot().ShouldContain(entry => entry.Message == ShuttingDownMessage);
        _channel.CallsToBasicConsumeAsync().MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task ExecuteAsync_ConsumerCancelledByBroker_LogsStopReasonAndClosesChannel()
    {
        // Arrange
        var consumer = await StartConsumingAsync();

        // Act
        await consumer.HandleBasicCancelAsync(RabbitMqDeliveries.ConsumerTag, TestContext.Current.CancellationToken);
        var stopMessage = $"RabbitMQ consumer of queue {QueueName} stopped. Consumer was cancelled by the broker. " +
            "Restarting in 5 seconds";
        await WaitForLogAsync(stopMessage);
        await _sut.StopAsync(TestContext.Current.CancellationToken);

        // Assert
        _logger.Collector.GetSnapshot().ShouldContain(entry => entry.Level == LogLevel.Warning && entry.Message == stopMessage);
        _channel.CallsToCloseAsync()
            .MustHaveHappenedOnceExactly()
            .Then(A.CallTo(() => _channel.DisposeAsync()).MustHaveHappenedOnceExactly());
    }

    [Fact]
    public async Task ExecuteAsync_StoppedWhileConsuming_ShutsDownGracefullyAndClosesChannel()
    {
        // Arrange
        await StartConsumingAsync();

        // Act
        await _sut.StopAsync(TestContext.Current.CancellationToken);

        // Assert
        _sut.ExecuteTask.ShouldNotBeNull().IsCanceled.ShouldBeTrue();
        _logger.Collector.GetSnapshot().ShouldContain(entry => entry.Level == LogLevel.Information && entry.Message == ShuttingDownMessage);
        _logger.Collector.GetSnapshot().ShouldNotContain(entry => entry.Level >= LogLevel.Warning);
        _channel.CallsToCloseAsync()
            .MustHaveHappenedOnceExactly()
            .Then(A.CallTo(() => _channel.DisposeAsync()).MustHaveHappenedOnceExactly());
    }

    [Fact]
    public async Task ExecuteAsync_StoppedWhileConnecting_ShutsDownGracefullyWithoutOpeningChannel()
    {
        // Arrange
        A.CallTo(() => _connectionProvider.GetConnectionAsync(A<CancellationToken>._))
            .ReturnsLazily(async (CancellationToken token) =>
            {
                await Task.Delay(Timeout.Infinite, token);

                return Result<IConnection>.Succeed(_connection);
            });

        // Act
        await _sut.StartAsync(TestContext.Current.CancellationToken);
        await TestWait.UntilAsync(() => Fake.GetCalls(_connectionProvider).Any(), TestContext.Current.CancellationToken);
        await _sut.StopAsync(TestContext.Current.CancellationToken);

        // Assert
        _sut.ExecuteTask.ShouldNotBeNull().IsCanceled.ShouldBeTrue();
        _logger.Collector.GetSnapshot().ShouldContain(entry => entry.Level == LogLevel.Information && entry.Message == ShuttingDownMessage);
        _logger.Collector.GetSnapshot().ShouldNotContain(entry => entry.Level >= LogLevel.Warning);
        A.CallTo(() => _connection.CreateChannelAsync(A<CreateChannelOptions?>._, A<CancellationToken>._))
            .MustNotHaveHappened();
    }

    [Fact]
    public async Task ExecuteAsync_StoppedWhileSettingUpQueue_ShutsDownGracefullyAndClosesChannel()
    {
        // Arrange
        A.CallTo(() => _setupQueue(A<IChannel>._, A<CancellationToken>._))
            .ReturnsLazily(async (IChannel _, CancellationToken token) =>
            {
                await Task.Delay(Timeout.Infinite, token);

                return Result.Succeed();
            });

        // Act
        await _sut.StartAsync(TestContext.Current.CancellationToken);
        await TestWait.UntilAsync(() => Fake.GetCalls(_setupQueue).Any(), TestContext.Current.CancellationToken);
        await _sut.StopAsync(TestContext.Current.CancellationToken);

        // Assert
        _logger.Collector.GetSnapshot().ShouldContain(entry => entry.Level == LogLevel.Information && entry.Message == ShuttingDownMessage);
        _logger.Collector.GetSnapshot().ShouldNotContain(entry => entry.Level >= LogLevel.Warning);
        _channel.CallsToBasicConsumeAsync().MustNotHaveHappened();
        A.CallTo(() => _channel.DisposeAsync()).MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task ExecuteAsync_ChannelCloseThrows_LogsWarningAndStillDisposesChannel()
    {
        // Arrange
        var exception = new InvalidOperationException("Channel is broken");
        _channel.CallsToCloseAsync().ThrowsAsync(exception);
        await StartConsumingAsync();

        // Act
        await _sut.StopAsync(TestContext.Current.CancellationToken);

        // Assert
        var warning = _logger.Collector.GetSnapshot().Where(entry => entry.Level == LogLevel.Warning).ShouldHaveSingleItem();
        warning.Message.ShouldBe("Failed to close the channel");
        warning.Exception.ShouldBeSameAs(exception);
        A.CallTo(() => _channel.DisposeAsync()).MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task MessageDelivered_AnyMessage_PassesItsPropertiesAndBodyToProcessing()
    {
        // Arrange
        var consumer = await StartConsumingAsync();
        var properties = RabbitMqDeliveries.Properties();
        var body = """{"location":"Kitchen"}"""u8.ToArray();
        ReceivedMessage? received = null;
        byte[]? receivedBody = null;
        A.CallTo(() => _processMessage(A<ReceivedMessage>._, A<CancellationToken>._))
            .Invokes((ReceivedMessage message, CancellationToken _) =>
            {
                // The body is only valid while processing runs, so it is copied.
                received = message;
                receivedBody = message.Body.ToArray();
            })
            .Returns(Result.Succeed());

        // Act
        await consumer.DeliverAsync(DeliveryTag, properties, body, TestContext.Current.CancellationToken);

        // Assert
        received.ShouldNotBeNull().BasicProperties.ShouldBeSameAs(properties);
        receivedBody.ShouldBe(body);
    }

    [Fact]
    public async Task MessageDelivered_ProcessingSucceeds_AcksMessageAndLogsNoWarnings()
    {
        // Arrange
        var consumer = await StartConsumingAsync();

        // Act
        await consumer.DeliverAsync(DeliveryTag, RabbitMqDeliveries.Properties(), TestContext.Current.CancellationToken);

        // Assert
        A.CallTo(() => _channel.BasicAckAsync(DeliveryTag, false, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
        _channel.CallsToBasicNackAsync().MustNotHaveHappened();
        _logger.Collector.GetSnapshot().ShouldNotContain(entry => entry.Level >= LogLevel.Warning);
        _logger.LatestRecord.Level.ShouldBe(LogLevel.Debug);
        _logger.LatestRecord.Message.ShouldBe(
            $"RabbitMQ message {RabbitMqDeliveries.MessageId} ({RabbitMqDeliveries.MessageType}) is acked");
    }

    [Fact]
    public async Task MessageDelivered_ProcessingReturnsFailure_NacksWithoutRequeueAndLogsReason()
    {
        // Arrange
        var consumer = await StartConsumingAsync();
        A.CallTo(() => _processMessage(A<ReceivedMessage>._, A<CancellationToken>._))
            .Returns(Result.Fail("Reading has no location"));

        // Act
        await consumer.DeliverAsync(DeliveryTag, RabbitMqDeliveries.Properties(), TestContext.Current.CancellationToken);

        // Assert
        A.CallTo(() => _channel.BasicNackAsync(DeliveryTag, false, false, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
        _channel.CallsToBasicAckAsync().MustNotHaveHappened();
        var warning = _logger.Collector.GetSnapshot().Where(entry => entry.Level == LogLevel.Warning).ShouldHaveSingleItem();
        warning.Message.ShouldBe(
            $"RabbitMQ message {RabbitMqDeliveries.MessageId} ({RabbitMqDeliveries.MessageType}) processing failed. " +
            "Reading has no location");
    }

    [Fact]
    public async Task MessageDelivered_ProcessingThrows_NacksWithoutRequeueAndLogsException()
    {
        // Arrange
        var consumer = await StartConsumingAsync();
        var exception = new InvalidOperationException("Unexpected failure");
        A.CallTo(() => _processMessage(A<ReceivedMessage>._, A<CancellationToken>._)).ThrowsAsync(exception);

        // Act
        await consumer.DeliverAsync(DeliveryTag, RabbitMqDeliveries.Properties(), TestContext.Current.CancellationToken);

        // Assert
        A.CallTo(() => _channel.BasicNackAsync(DeliveryTag, false, false, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
        _channel.CallsToBasicAckAsync().MustNotHaveHappened();
        var warning = _logger.Collector.GetSnapshot().Where(entry => entry.Level == LogLevel.Warning).ShouldHaveSingleItem();
        warning.Message.ShouldBe(
            $"RabbitMQ message {RabbitMqDeliveries.MessageId} ({RabbitMqDeliveries.MessageType}) processing failed");
        warning.Exception.ShouldBeSameAs(exception);
    }

    [Fact]
    public async Task MessageDelivered_ProcessingThrowsCancellationNotCausedByTokens_TreatsItAsFailureWithoutRequeue()
    {
        // Arrange
        var consumer = await StartConsumingAsync();
        var exception = new TaskCanceledException("The database command timed out");
        A.CallTo(() => _processMessage(A<ReceivedMessage>._, A<CancellationToken>._)).ThrowsAsync(exception);

        // Act
        await consumer.DeliverAsync(DeliveryTag, RabbitMqDeliveries.Properties(), TestContext.Current.CancellationToken);

        // Assert
        A.CallTo(() => _channel.BasicNackAsync(DeliveryTag, false, false, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
        var warning = _logger.Collector.GetSnapshot().Where(entry => entry.Level == LogLevel.Warning).ShouldHaveSingleItem();
        warning.Exception.ShouldBeSameAs(exception);
    }

    [Fact]
    public async Task MessageDelivered_DeliveryCancelledWhileProcessing_NacksWithRequeue()
    {
        // Arrange
        var consumer = await StartConsumingAsync();
        using var deliveryCts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        A.CallTo(() => _processMessage(A<ReceivedMessage>._, A<CancellationToken>._))
            .ReturnsLazily(async (ReceivedMessage _, CancellationToken token) =>
            {
                await deliveryCts.CancelAsync();

                await Task.Delay(Timeout.Infinite, token);

                return Result.Succeed();
            });

        // Act
        await consumer.DeliverAsync(DeliveryTag, RabbitMqDeliveries.Properties(), deliveryCts.Token);

        // Assert
        A.CallTo(() => _channel.BasicNackAsync(DeliveryTag, false, true, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
        _channel.CallsToBasicAckAsync().MustNotHaveHappened();
        var warning = _logger.Collector.GetSnapshot().Where(entry => entry.Level == LogLevel.Warning).ShouldHaveSingleItem();
        warning.Message.ShouldBe(
            $"RabbitMQ message {RabbitMqDeliveries.MessageId} ({RabbitMqDeliveries.MessageType}) processing failed. " +
            "Message processing cancelled");
    }

    [Fact]
    public async Task MessageDelivered_StoppedWhileProcessing_NacksWithRequeue()
    {
        // Arrange
        var consumer = await StartConsumingAsync();
        A.CallTo(() => _processMessage(A<ReceivedMessage>._, A<CancellationToken>._))
            .ReturnsLazily(async (ReceivedMessage _, CancellationToken token) =>
            {
                await Task.Delay(Timeout.Infinite, token);

                return Result.Succeed();
            });

        // Act
        var delivery = consumer.DeliverAsync(DeliveryTag, RabbitMqDeliveries.Properties(), TestContext.Current.CancellationToken);
        await TestWait.UntilAsync(() => Fake.GetCalls(_processMessage).Any(), TestContext.Current.CancellationToken);
        await _sut.StopAsync(TestContext.Current.CancellationToken);
        await delivery;

        // Assert
        A.CallTo(() => _channel.BasicNackAsync(DeliveryTag, false, true, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
        _channel.CallsToBasicAckAsync().MustNotHaveHappened();
        _logger.Collector.GetSnapshot().ShouldContain(entry =>
            entry.Level == LogLevel.Warning &&
            entry.Message == $"RabbitMQ message {RabbitMqDeliveries.MessageId} ({RabbitMqDeliveries.MessageType}) processing failed. " +
                "Consumer shutting down");
    }

    [Fact]
    public async Task MessageDelivered_AckThrows_LogsWarningWithoutFailingDelivery()
    {
        // Arrange
        var consumer = await StartConsumingAsync();
        var exception = new InvalidOperationException("Channel is closed");
        _channel.CallsToBasicAckAsync().ThrowsAsync(exception);

        // Act
        await consumer.DeliverAsync(DeliveryTag, RabbitMqDeliveries.Properties(), TestContext.Current.CancellationToken);

        // Assert
        var warning = _logger.Collector.GetSnapshot().Where(entry => entry.Level == LogLevel.Warning).ShouldHaveSingleItem();
        warning.Message.ShouldBe($"Failed to ack RabbitMQ message {RabbitMqDeliveries.MessageId} ({RabbitMqDeliveries.MessageType})");
        warning.Exception.ShouldBeSameAs(exception);
        _channel.CallsToBasicNackAsync().MustNotHaveHappened();
    }

    [Fact]
    public async Task MessageDelivered_NackThrows_LogsWarningWithoutFailingDelivery()
    {
        // Arrange
        var consumer = await StartConsumingAsync();
        var exception = new InvalidOperationException("Channel is closed");
        A.CallTo(() => _processMessage(A<ReceivedMessage>._, A<CancellationToken>._))
            .Returns(Result.Fail("Reading has no location"));
        _channel.CallsToBasicNackAsync().ThrowsAsync(exception);

        // Act
        await consumer.DeliverAsync(DeliveryTag, RabbitMqDeliveries.Properties(), TestContext.Current.CancellationToken);

        // Assert
        _logger.LatestRecord.Level.ShouldBe(LogLevel.Warning);
        _logger.LatestRecord.Message.ShouldBe(
            $"Failed to nack RabbitMQ message {RabbitMqDeliveries.MessageId} ({RabbitMqDeliveries.MessageType})");
        _logger.LatestRecord.Exception.ShouldBeSameAs(exception);
    }

    [Fact]
    public async Task MessageDelivered_ValidCorrelationId_ProcessesMessageUnderIt()
    {
        // Arrange
        var consumer = await StartConsumingAsync();
        var correlationId = Guid.CreateVersion7();
        var processingCorrelationId = Guid.Empty;
        A.CallTo(() => _processMessage(A<ReceivedMessage>._, A<CancellationToken>._))
            .Invokes(() => processingCorrelationId = CorrelationContext.CorrelationId)
            .Returns(Result.Succeed());

        // Act
        await consumer.DeliverAsync(
            DeliveryTag,
            RabbitMqDeliveries.Properties(correlationId.ToString()),
            TestContext.Current.CancellationToken);

        // Assert
        processingCorrelationId.ShouldBe(correlationId);
        _logger.Collector.GetSnapshot().ShouldNotContain(entry => entry.Level >= LogLevel.Warning);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-a-guid")]
    public async Task MessageDelivered_MissingOrInvalidCorrelationId_ProcessesMessageUnderNewVersion7IdAndLogsWarning(
        string? receivedCorrelationId)
    {
        // Arrange
        var consumer = await StartConsumingAsync();
        var properties = RabbitMqDeliveries.Properties();
        properties.CorrelationId = receivedCorrelationId;
        var processingCorrelationId = Guid.Empty;
        A.CallTo(() => _processMessage(A<ReceivedMessage>._, A<CancellationToken>._))
            .Invokes(() => processingCorrelationId = CorrelationContext.CorrelationId)
            .Returns(Result.Succeed());

        // Act
        await consumer.DeliverAsync(DeliveryTag, properties, TestContext.Current.CancellationToken);

        // Assert
        processingCorrelationId.Version.ShouldBe(7);
        var warning = _logger.Collector.GetSnapshot().Where(entry => entry.Level == LogLevel.Warning).ShouldHaveSingleItem();
        warning.Message.ShouldBe(
            $"Failed to restore correlation ID from RabbitMQ message. Received correlation ID: {receivedCorrelationId ?? "(null)"}");
        A.CallTo(() => _channel.BasicAckAsync(DeliveryTag, false, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task MessageDelivered_SeveralMessagesWithoutCorrelationId_ProcessesEachUnderItsOwnId()
    {
        // Arrange
        var consumer = await StartConsumingAsync();
        var processingCorrelationIds = new List<Guid>();
        A.CallTo(() => _processMessage(A<ReceivedMessage>._, A<CancellationToken>._))
            .Invokes(() => processingCorrelationIds.Add(CorrelationContext.CorrelationId))
            .Returns(Result.Succeed());
        var properties = RabbitMqDeliveries.Properties();
        properties.CorrelationId = null;

        // Act
        await consumer.DeliverAsync(DeliveryTag, properties, TestContext.Current.CancellationToken);
        await consumer.DeliverAsync(DeliveryTag + 1, properties, TestContext.Current.CancellationToken);

        // Assert
        processingCorrelationIds.Count.ShouldBe(2);
        processingCorrelationIds[1].ShouldNotBe(processingCorrelationIds[0]);
    }

    private async Task<IAsyncBasicConsumer> StartConsumingAsync()
    {
        await _sut.StartAsync(TestContext.Current.CancellationToken);

        await TestWait.UntilAsync(() => _channel.RegisteredConsumers().Count > 0, TestContext.Current.CancellationToken);

        return _channel.RegisteredConsumers().ShouldHaveSingleItem();
    }

    private Task WaitForLogAsync(string message)
    {
        return TestWait.UntilAsync(
            () => _logger.Collector.GetSnapshot().Any(entry => entry.Message == message),
            TestContext.Current.CancellationToken);
    }
}
