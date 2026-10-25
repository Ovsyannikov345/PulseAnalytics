using System.Text.Json;
using Ausiannikau.Utilities.Results;
using DataIngestor.Contracts.Events;
using DataIngestor.Domain.Entities;
using DataIngestor.Infrastructure.RabbitMq.Configuration;
using DataIngestor.Infrastructure.RabbitMq.Connection;
using DataIngestor.Infrastructure.RabbitMq.Publishers;
using DataIngestor.UnitTests.TestDoubles;
using DataIngestor.UnitTests.Utilities;
using FakeItEasy;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Exceptions;
using Shouldly;
using Xunit;

namespace DataIngestor.UnitTests.Infrastructure.RabbitMq.Publishers;

public sealed class MeterReadingsPublisherTests
{
    private const string ExchangeName = "pulse.meter-readings";
    private const int PublishTimeoutInSeconds = 5;

    private static readonly DateTimeOffset Now = new(2026, 10, 24, 12, 0, 5, TimeSpan.Zero);

    private readonly IRabbitMqConnectionProvider _connectionProvider = A.Fake<IRabbitMqConnectionProvider>();
    private readonly IConnection _connection = A.Fake<IConnection>();
    private readonly IChannel _channel = A.Fake<IChannel>();
    private readonly FakeTimeProvider _timeProvider = new(Now);
    private readonly FakeLogger<MeterReadingsPublisher> _logger = new();
    private readonly MeterReadingsPublisher _sut;

    public MeterReadingsPublisherTests()
    {
        A.CallTo(() => _connectionProvider.GetConnectionAsync(A<CancellationToken>._))
            .Returns(Result<IConnection>.Succeed(_connection));

        A.CallTo(() => _connection.CreateChannelAsync(A<CreateChannelOptions?>._, A<CancellationToken>._))
            .Returns(_channel);

        var settings = new RabbitMqSettings
        {
            Host = "rabbitmq.test",
            Port = 5672,
            VirtualHost = "/",
            UserName = "ingestor",
            Password = "secret",
            PublishTimeoutInSeconds = PublishTimeoutInSeconds
        };

        _sut = new MeterReadingsPublisher(_connectionProvider, Options.Create(settings), _timeProvider, _logger);
    }

    [Fact]
    public async Task PublishAsync_AnyBatch_OpensChannelWithPublisherConfirmationsTracked()
    {
        // Act
        await _sut.PublishAsync([MeterReadings.Motion()], TestContext.Current.CancellationToken);

        // Assert
        A.CallTo(() => _connection.CreateChannelAsync(
                A<CreateChannelOptions?>.That.Matches(options =>
                    options != null && options.PublisherConfirmationsEnabled && options.PublisherConfirmationTrackingEnabled),
                A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task PublishAsync_AnyBatch_DeclaresDurableTopicExchangeBeforePublishing()
    {
        // Act
        await _sut.PublishAsync([MeterReadings.Motion()], TestContext.Current.CancellationToken);

        // Assert
        A.CallTo(() => _channel.ExchangeDeclareAsync(
                ExchangeName,
                ExchangeType.Topic,
                true,
                false,
                A<IDictionary<string, object?>?>._,
                A<bool>._,
                A<bool>._,
                A<CancellationToken>._))
            .MustHaveHappenedOnceExactly()
            .Then(_channel.CallsToBasicPublishAsync().MustHaveHappened());
    }

    [Fact]
    public async Task PublishAsync_ReadingsOfAllTypes_PublishesEachInOrderWithItsRoutingKey()
    {
        // Arrange
        MeterReading[] readings = [MeterReadings.AirQuality(), MeterReadings.Motion(), MeterReadings.Energy()];

        // Act
        await _sut.PublishAsync(readings, TestContext.Current.CancellationToken);

        // Assert
        var published = _channel.PublishedMessages();
        published.ShouldAllBe(message => message.Exchange == ExchangeName);
        published.Select(message => message.RoutingKey).ShouldBe(
        [
            "meter-reading.air-quality.received",
            "meter-reading.motion.received",
            "meter-reading.energy.received"
        ]);
    }

    [Fact]
    public async Task PublishAsync_AnyReading_PublishesMandatoryPersistentJsonMessageWithMetadata()
    {
        // Act
        await _sut.PublishAsync([MeterReadings.Energy()], TestContext.Current.CancellationToken);

        // Assert
        var published = _channel.PublishedMessages().ShouldHaveSingleItem();
        published.Mandatory.ShouldBeTrue();

        var publishedEvent = JsonSerializer.Deserialize<EnergyReadingReceived>(published.Body, JsonSerializerOptions.Web)!;
        published.Properties.MessageId.ShouldBe(publishedEvent.MessageId.ToString());
        published.Properties.Type.ShouldBe(nameof(EnergyReadingReceived));
        published.Properties.ContentType.ShouldBe("application/json");
        published.Properties.DeliveryMode.ShouldBe(DeliveryModes.Persistent);
        published.Properties.AppId.ShouldBe("data-ingestor");
        published.Properties.Timestamp.UnixTime.ShouldBe(Now.ToUnixTimeSeconds());
    }

    [Fact]
    public async Task PublishAsync_AnyReading_SerializesEventOfItsRuntimeTypeAsCamelCaseJson()
    {
        // Act
        await _sut.PublishAsync([MeterReadings.AirQuality("Kitchen", co2: 803, pm25: 25, humidity: 36)], TestContext.Current.CancellationToken);

        // Assert
        var body = _channel.PublishedMessages().ShouldHaveSingleItem().Body;

        using var document = JsonDocument.Parse(body);
        document.RootElement.EnumerateObject().Select(property => property.Name).ShouldBe(
            ["co2", "pm25", "humidity", "messageId", "location", "receivedAt"],
            ignoreOrder: true);

        var publishedEvent = JsonSerializer.Deserialize<AirQualityReadingReceived>(body, JsonSerializerOptions.Web);
        publishedEvent.ShouldBe(new AirQualityReadingReceived
        {
            MessageId = publishedEvent!.MessageId,
            Location = "Kitchen",
            ReceivedAt = MeterReadings.ReceivedAt,
            Co2 = 803,
            Pm25 = 25,
            Humidity = 36
        });
    }

    [Fact]
    public async Task PublishAsync_AllReadingsPublished_ReturnsSuccessAndLogsPublishedCount()
    {
        // Act
        var result = await _sut.PublishAsync(
            [MeterReadings.Motion(), MeterReadings.Energy(), MeterReadings.AirQuality()],
            TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        _logger.Collector.GetSnapshot().ShouldNotContain(entry => entry.Level >= LogLevel.Warning);
        _logger.LatestRecord.Level.ShouldBe(LogLevel.Information);
        _logger.LatestRecord.Message.ShouldBe("Published 3 meter readings");
    }

    [Fact]
    public async Task PublishAsync_AnyBatch_DisposesChannel()
    {
        // Act
        await _sut.PublishAsync([MeterReadings.Motion()], TestContext.Current.CancellationToken);

        // Assert
        A.CallTo(() => _channel.DisposeAsync()).MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task PublishAsync_AnyBatch_PassesCancellationTokenToConnectionProvider()
    {
        // Arrange
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);

        // Act
        await _sut.PublishAsync([MeterReadings.Motion()], cts.Token);

        // Assert
        A.CallTo(() => _connectionProvider.GetConnectionAsync(cts.Token)).MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task PublishAsync_ConnectionUnavailable_ReturnsConnectionFailureWithoutOpeningChannel()
    {
        // Arrange
        A.CallTo(() => _connectionProvider.GetConnectionAsync(A<CancellationToken>._))
            .Returns(Result<IConnection>.Fail("Failed to connect to RabbitMQ"));

        // Act
        var result = await _sut.PublishAsync([MeterReadings.Motion()], TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.ErrorMessage.ShouldBe("Failed to connect to RabbitMQ");
        A.CallTo(_connection).MustNotHaveHappened();
    }

    [Fact]
    public async Task PublishAsync_ChannelCannotBeOpened_ReturnsFailureAndLogsException()
    {
        // Arrange
        var exception = new InvalidOperationException("Channel limit reached");
        A.CallTo(() => _connection.CreateChannelAsync(A<CreateChannelOptions?>._, A<CancellationToken>._)).ThrowsAsync(exception);

        // Act
        var result = await _sut.PublishAsync([MeterReadings.Motion()], TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.ErrorMessage.ShouldBe("Failed to open RabbitMQ channel");
        var error = _logger.Collector.GetSnapshot().ShouldHaveSingleItem();
        error.Level.ShouldBe(LogLevel.Error);
        error.Message.ShouldBe("Exception occurred while opening RabbitMQ channel");
        error.Exception.ShouldBeSameAs(exception);
    }

    [Fact]
    public async Task PublishAsync_CallerCancelsWhileOpeningChannel_ThrowsOperationCanceledException()
    {
        // Arrange
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        A.CallTo(() => _connection.CreateChannelAsync(A<CreateChannelOptions?>._, A<CancellationToken>._))
            .ReturnsLazily(async () =>
            {
                await cts.CancelAsync();

                cts.Token.ThrowIfCancellationRequested();

                return _channel;
            });

        // Act
        var act = () => _sut.PublishAsync([MeterReadings.Motion()], cts.Token);

        // Assert
        await act.ShouldThrowAsync<OperationCanceledException>();
        _logger.Collector.GetSnapshot().ShouldNotContain(entry => entry.Level >= LogLevel.Warning);
    }

    [Fact]
    public async Task PublishAsync_ExchangeCannotBeDeclared_ReturnsFailureWithoutPublishing()
    {
        // Arrange
        var exception = new InvalidOperationException("PRECONDITION_FAILED - inequivalent arg 'type'");
        _channel.CallsToExchangeDeclareAsync().ThrowsAsync(exception);

        // Act
        var result = await _sut.PublishAsync([MeterReadings.Motion()], TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.ErrorMessage.ShouldBe($"Failed to declare RabbitMQ exchange {ExchangeName}");
        _channel.CallsToBasicPublishAsync().MustNotHaveHappened();
        A.CallTo(() => _channel.DisposeAsync()).MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task PublishAsync_ExchangeCannotBeDeclared_LogsErrorWithException()
    {
        // Arrange
        var exception = new InvalidOperationException("PRECONDITION_FAILED - inequivalent arg 'type'");
        _channel.CallsToExchangeDeclareAsync().ThrowsAsync(exception);

        // Act
        await _sut.PublishAsync([MeterReadings.Motion()], TestContext.Current.CancellationToken);

        // Assert
        var error = _logger.Collector.GetSnapshot().ShouldHaveSingleItem();
        error.Level.ShouldBe(LogLevel.Error);
        error.Message.ShouldBe($"Exception occurred while declaring RabbitMQ exchange {ExchangeName} of type topic");
        error.Exception.ShouldBeSameAs(exception);
    }

    [Fact]
    public async Task PublishAsync_MessageReturnedAsUnroutable_ReturnsFailureAndLogsWarningWithReply()
    {
        // Arrange
        const string routingKey = "meter-reading.motion.received";
        _channel.CallsToBasicPublishAsync()
            .ThrowsAsync(new PublishReturnException(1, "Message returned", ExchangeName, routingKey, 312, "NO_ROUTE"));

        // Act
        var result = await _sut.PublishAsync([MeterReadings.Motion()], TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.ErrorMessage.ShouldBe("Failed to publish 1 of 1 meter readings");
        var messageId = SinglePublishedMessageId();
        var warnings = _logger.Collector.GetSnapshot().Where(entry => entry.Level == LogLevel.Warning).ToList();
        warnings.ShouldContain(entry => entry.Message ==
            $"MotionReadingReceived {messageId} was returned as unroutable. Exchange: {ExchangeName}. " +
            $"Routing key: {routingKey}. Reply code: 312. Reply text: NO_ROUTE");
        _logger.Collector.GetSnapshot().ShouldNotContain(entry => entry.Level == LogLevel.Error);
    }

    [Fact]
    public async Task PublishAsync_MessageNackedByBroker_ReturnsFailureAndLogsError()
    {
        // Arrange
        var exception = new PublishException(1, isReturn: false);
        _channel.CallsToBasicPublishAsync().ThrowsAsync(exception);

        // Act
        var result = await _sut.PublishAsync([MeterReadings.Energy()], TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.ErrorMessage.ShouldBe("Failed to publish 1 of 1 meter readings");
        var messageId = SinglePublishedMessageId();
        var error = _logger.Collector.GetSnapshot().Where(entry => entry.Level == LogLevel.Error).ShouldHaveSingleItem();
        error.Message.ShouldBe(
            $"EnergyReadingReceived {messageId} was nacked by the broker. Exchange: {ExchangeName}. " +
            "Routing key: meter-reading.energy.received");
        error.Exception.ShouldBeSameAs(exception);
    }

    [Fact]
    public async Task PublishAsync_PublishThrowsUnexpectedException_ReturnsFailureAndLogsError()
    {
        // Arrange
        var exception = new InvalidOperationException("Channel is broken");
        _channel.CallsToBasicPublishAsync().ThrowsAsync(exception);

        // Act
        var result = await _sut.PublishAsync([MeterReadings.AirQuality()], TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.ErrorMessage.ShouldBe("Failed to publish 1 of 1 meter readings");
        var messageId = SinglePublishedMessageId();
        var error = _logger.Collector.GetSnapshot().Where(entry => entry.Level == LogLevel.Error).ShouldHaveSingleItem();
        error.Message.ShouldBe(
            $"Exception occurred while publishing AirQualityReadingReceived {messageId}. Exchange: {ExchangeName}. " +
            "Routing key: meter-reading.air-quality.received");
        error.Exception.ShouldBeSameAs(exception);
    }

    [Fact]
    public async Task PublishAsync_OneOfSeveralPublishesFails_PublishesTheRestAndReportsFailedCount()
    {
        // Arrange
        _channel.CallsToBasicPublishAsync()
            .ThrowsAsync(new PublishException(1, isReturn: false)).Once()
            .Then.Returns(ValueTask.CompletedTask);

        // Act
        var result = await _sut.PublishAsync(
            [MeterReadings.Motion("Garage"), MeterReadings.Energy(), MeterReadings.AirQuality()],
            TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.ErrorMessage.ShouldBe("Failed to publish 1 of 3 meter readings");
        _channel.PublishedMessages().Count.ShouldBe(3);
        var failedMessageId = _channel.PublishedMessages()[0].Properties.MessageId;
        _logger.Collector.GetSnapshot().ShouldContain(entry =>
            entry.Level == LogLevel.Warning &&
            entry.Message == $"Failed to publish MotionReadingReceived {failedMessageId} for location Garage. " +
                "Reason: Message was rejected by RabbitMQ");
    }

    [Fact]
    public async Task PublishAsync_ChannelClosesAfterFailedPublish_SkipsRemainingReadings()
    {
        // Arrange
        _channel.CallsToBasicPublishAsync().ThrowsAsync(new AlreadyClosedException(CreateShutdownArgs()));
        A.CallTo(() => _channel.IsClosed).ReturnsNextFromSequence(false, true);

        // Act
        var result = await _sut.PublishAsync(
            [MeterReadings.Motion(), MeterReadings.Energy(), MeterReadings.AirQuality()],
            TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.ErrorMessage.ShouldBe("Failed to publish 3 of 3 meter readings");
        _channel.PublishedMessages().ShouldHaveSingleItem();
        _logger.Collector.GetSnapshot().ShouldContain(entry =>
            entry.Level == LogLevel.Error &&
            entry.Message == "RabbitMQ channel was closed while publishing, 2 remaining meter readings were not published");
    }

    [Fact]
    public async Task PublishAsync_ConfirmNotReceivedWithinTimeout_ReturnsFailureAndLogsTimeout()
    {
        // Arrange
        _channel.HangsUntilPublishIsCancelled();

        // Act
        var publishTask = _sut.PublishAsync([MeterReadings.Motion()], TestContext.Current.CancellationToken);
        await TestWait.UntilAsync(() => _channel.PublishedMessages().Count == 1, TestContext.Current.CancellationToken);

        _timeProvider.Advance(TimeSpan.FromSeconds(PublishTimeoutInSeconds) - TimeSpan.FromMilliseconds(1));
        var completedBeforeTimeout = publishTask.IsCompleted;

        _timeProvider.Advance(TimeSpan.FromMilliseconds(1));
        var result = await publishTask;

        // Assert
        completedBeforeTimeout.ShouldBeFalse();
        result.IsFailure.ShouldBeTrue();
        result.ErrorMessage.ShouldBe("Failed to publish 1 of 1 meter readings");
        var messageId = SinglePublishedMessageId();
        _logger.Collector.GetSnapshot().ShouldContain(entry =>
            entry.Level == LogLevel.Error &&
            entry.Message == $"MotionReadingReceived {messageId} was not confirmed within {PublishTimeoutInSeconds} seconds. " +
                $"Exchange: {ExchangeName}. Routing key: meter-reading.motion.received");
    }

    [Fact]
    public async Task PublishAsync_CallerCancelsWhilePublishIsInFlight_ThrowsOperationCanceledException()
    {
        // Arrange
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        _channel.CancelsCallerWhilePublishing(cts);

        // Act
        var act = () => _sut.PublishAsync([MeterReadings.Motion(), MeterReadings.Energy()], cts.Token);

        // Assert
        await act.ShouldThrowAsync<OperationCanceledException>();
        _channel.PublishedMessages().ShouldHaveSingleItem();
        _logger.Collector.GetSnapshot().ShouldNotContain(entry => entry.Level >= LogLevel.Warning);
    }

    private static ShutdownEventArgs CreateShutdownArgs()
    {
        return new ShutdownEventArgs(ShutdownInitiator.Peer, 320, "CONNECTION_FORCED");
    }

    private string SinglePublishedMessageId()
    {
        return _channel.PublishedMessages().ShouldHaveSingleItem().Properties.MessageId!;
    }
}
