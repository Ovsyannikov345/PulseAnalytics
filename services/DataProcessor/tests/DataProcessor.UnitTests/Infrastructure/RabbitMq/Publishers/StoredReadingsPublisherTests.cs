using System.Text.Json;
using Ausiannikau.Utilities.Results;
using DataProcessor.Application.Correlation;
using DataProcessor.Application.Telemetry.Metrics;
using DataProcessor.Application.Telemetry.Metrics.Enums;
using DataProcessor.Contracts.Events;
using DataProcessor.Domain.Constants.Enums;
using DataProcessor.Domain.Entities.MeterReadings;
using DataProcessor.Domain.Entities.MeterReadings.Payloads;
using DataProcessor.Infrastructure.RabbitMq.Configuration;
using DataProcessor.Infrastructure.RabbitMq.Connection;
using DataProcessor.Infrastructure.RabbitMq.Publishers;
using DataProcessor.UnitTests.TestDoubles;
using DataProcessor.UnitTests.Utilities;
using FakeItEasy;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;
using Shouldly;
using Xunit;
using ContractMeterLocation = DataProcessor.Contracts.Enums.MeterLocation;

namespace DataProcessor.UnitTests.Infrastructure.RabbitMq.Publishers;

public sealed class StoredReadingsPublisherTests : IAsyncDisposable
{
    private const string ExchangeName = "pulse.meter-readings.stored";
    private const int PublishTimeoutInSeconds = 5;

    private static readonly DateTimeOffset Now = new(2026, 11, 17, 10, 15, 35, TimeSpan.Zero);

    private static readonly DateTimeOffset ReceivedAt = new(2026, 11, 17, 10, 15, 30, TimeSpan.Zero);

    private readonly IRabbitMqConnectionProvider _connectionProvider = A.Fake<IRabbitMqConnectionProvider>();
    private readonly IConnection _connection = A.Fake<IConnection>();
    private readonly IChannel _channel = A.Fake<IChannel>();
    private readonly FakeTimeProvider _timeProvider = new(Now);
    private readonly IMetricsService _metrics = A.Fake<IMetricsService>();
    private readonly FakeLogger<StoredReadingsPublisher> _logger = new();
    private readonly StoredReadingsPublisher _sut;

    public StoredReadingsPublisherTests()
    {
        A.CallTo(() => _connectionProvider.GetConnectionAsync(A<CancellationToken>._))
            .Returns(Result<IConnection>.Succeed(_connection));

        A.CallTo(() => _connection.CreateChannelAsync(A<CreateChannelOptions?>._, A<CancellationToken>._))
            .Returns(_channel);

        A.CallTo(() => _channel.IsOpen).Returns(true);

        var settings = new RabbitMqSettings
        {
            Host = "rabbitmq.test",
            Port = 5672,
            VirtualHost = "/",
            UserName = "processor",
            Password = "secret",
            ClientProvidedName = "pulse-analytics.data-processor",
            PublishTimeoutInSeconds = PublishTimeoutInSeconds
        };

        _sut = new StoredReadingsPublisher(_connectionProvider, Options.Create(settings), _timeProvider, _metrics, _logger);
    }

    public async ValueTask DisposeAsync()
    {
        await _sut.DisposeAsync();
    }

    [Fact]
    public async Task PublishAsync_FirstReading_OpensChannelWithPublisherConfirmationsTracked()
    {
        // Act
        await _sut.PublishAsync(MotionReading(), TestContext.Current.CancellationToken);

        // Assert
        A.CallTo(() => _connection.CreateChannelAsync(
                A<CreateChannelOptions?>.That.Matches(options =>
                    options != null && options.PublisherConfirmationsEnabled && options.PublisherConfirmationTrackingEnabled),
                A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task PublishAsync_FirstReading_DeclaresDurableTopicExchangeBeforePublishing()
    {
        // Act
        await _sut.PublishAsync(MotionReading(), TestContext.Current.CancellationToken);

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
    public async Task PublishAsync_ReadingsOfAllTypes_PublishesEachWithItsRoutingKey()
    {
        // Act
        await _sut.PublishAsync(AirQualityReading(), TestContext.Current.CancellationToken);
        await _sut.PublishAsync(MotionReading(), TestContext.Current.CancellationToken);
        await _sut.PublishAsync(EnergyReading(), TestContext.Current.CancellationToken);

        // Assert
        var published = _channel.PublishedMessages();
        published.ShouldAllBe(message => message.Exchange == ExchangeName);
        published.Select(message => message.RoutingKey).ShouldBe(
        [
            "meter-reading.air-quality.stored",
            "meter-reading.motion.stored",
            "meter-reading.energy.stored"
        ]);
    }

    [Fact]
    public async Task PublishAsync_AnyReading_PublishesTransientNonMandatoryJsonMessageWithMetadata()
    {
        // Act
        await _sut.PublishAsync(EnergyReading(), TestContext.Current.CancellationToken);

        // Assert
        var published = _channel.PublishedMessages().ShouldHaveSingleItem();
        published.Mandatory.ShouldBeFalse();

        var publishedEvent = JsonSerializer.Deserialize<EnergyReadingStored>(published.Body, JsonSerializerOptions.Web)!;
        published.Properties.MessageId.ShouldBe(publishedEvent.MessageId.ToString());
        published.Properties.Type.ShouldBe(nameof(EnergyReadingStored));
        published.Properties.ContentType.ShouldBe("application/json");
        published.Properties.DeliveryMode.ShouldBe(DeliveryModes.Transient);
        published.Properties.AppId.ShouldBe("data-processor");
        published.Properties.Timestamp.UnixTime.ShouldBe(Now.ToUnixTimeSeconds());
    }

    [Fact]
    public async Task PublishAsync_AnyReading_SetsCurrentCorrelationId()
    {
        // Arrange
        var correlationId = Guid.CreateVersion7();
        CorrelationContext.CorrelationId = correlationId;

        // Act
        await _sut.PublishAsync(MotionReading(), TestContext.Current.CancellationToken);

        // Assert
        _channel.PublishedMessages().ShouldHaveSingleItem().Properties.CorrelationId.ShouldBe(correlationId.ToString());
    }

    [Fact]
    public async Task PublishAsync_AnyReading_SerializesEventOfItsRuntimeTypeAsCamelCaseJson()
    {
        // Arrange
        var reading = AirQualityReading();

        // Act
        await _sut.PublishAsync(reading, TestContext.Current.CancellationToken);

        // Assert
        var body = _channel.PublishedMessages().ShouldHaveSingleItem().Body;

        using var document = JsonDocument.Parse(body);
        document.RootElement.EnumerateObject().Select(property => property.Name).ShouldBe(
            ["co2", "pm25", "humidity", "messageId", "readingId", "location", "receivedAt"],
            ignoreOrder: true);
        document.RootElement.GetProperty("location").GetString().ShouldBe("LivingRoom");

        var publishedEvent = JsonSerializer.Deserialize<AirQualityReadingStored>(body, JsonSerializerOptions.Web);
        publishedEvent.ShouldBe(new AirQualityReadingStored
        {
            MessageId = publishedEvent!.MessageId,
            ReadingId = reading.Id,
            Location = ContractMeterLocation.LivingRoom,
            ReceivedAt = ReceivedAt,
            Co2 = 803,
            Pm25 = 25,
            Humidity = 36
        });
    }

    [Fact]
    public async Task PublishAsync_Confirmed_ReturnsSuccessWithoutWarningsAndRecordsConfirmedPublish()
    {
        // Act
        var result = await _sut.PublishAsync(MotionReading(), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        _logger.Collector.GetSnapshot().ShouldNotContain(entry => entry.Level >= LogLevel.Warning);
        RecordedPublishes().ShouldBe([("MotionReadingStored", PublishOutcome.Confirmed)]);
    }

    [Fact]
    public async Task PublishAsync_SeveralReadings_ReusesOneChannelAndDeclaresExchangeOnce()
    {
        // Act
        await _sut.PublishAsync(MotionReading(), TestContext.Current.CancellationToken);
        await _sut.PublishAsync(EnergyReading(), TestContext.Current.CancellationToken);
        await _sut.PublishAsync(AirQualityReading(), TestContext.Current.CancellationToken);

        // Assert
        A.CallTo(() => _connection.CreateChannelAsync(A<CreateChannelOptions?>._, A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
        _channel.CallsToExchangeDeclareAsync().MustHaveHappenedOnceExactly();
        _channel.PublishedMessages().Count.ShouldBe(3);
        A.CallTo(() => _channel.DisposeAsync()).MustNotHaveHappened();
    }

    [Fact]
    public async Task PublishAsync_ChannelClosedSinceLastPublish_DisposesItAndPublishesOnNewChannel()
    {
        // Arrange
        var newChannel = A.Fake<IChannel>();
        A.CallTo(() => newChannel.IsOpen).Returns(true);
        A.CallTo(() => _connection.CreateChannelAsync(A<CreateChannelOptions?>._, A<CancellationToken>._))
            .ReturnsNextFromSequence(_channel, newChannel);
        await _sut.PublishAsync(MotionReading(), TestContext.Current.CancellationToken);
        A.CallTo(() => _channel.IsOpen).Returns(false);

        // Act
        var result = await _sut.PublishAsync(EnergyReading(), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        A.CallTo(() => _channel.DisposeAsync()).MustHaveHappenedOnceExactly();
        _channel.PublishedMessages().ShouldHaveSingleItem();
        newChannel.CallsToExchangeDeclareAsync().MustHaveHappenedOnceExactly();
        newChannel.PublishedMessages().ShouldHaveSingleItem().RoutingKey.ShouldBe("meter-reading.energy.stored");
    }

    [Fact]
    public async Task PublishAsync_CalledConcurrently_PublishesOneAtATime()
    {
        // Arrange
        var inFlight = 0;
        var maxInFlight = 0;
        _channel.CallsToBasicPublishAsync().ReturnsLazily(() => new ValueTask(TrackAsync()));

        async Task TrackAsync()
        {
            var current = Interlocked.Increment(ref inFlight);
            maxInFlight = Math.Max(maxInFlight, current);

            await Task.Delay(20, TestContext.Current.CancellationToken);

            Interlocked.Decrement(ref inFlight);
        }

        // Act
        await Task.WhenAll(Enumerable.Range(0, 5)
            .Select(_ => _sut.PublishAsync(MotionReading(), TestContext.Current.CancellationToken)));

        // Assert
        maxInFlight.ShouldBe(1);
        _channel.PublishedMessages().Count.ShouldBe(5);
        A.CallTo(() => _connection.CreateChannelAsync(A<CreateChannelOptions?>._, A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task PublishAsync_ConnectionUnavailable_ReturnsFailureAndRecordsSkippedPublish()
    {
        // Arrange
        A.CallTo(() => _connectionProvider.GetConnectionAsync(A<CancellationToken>._))
            .Returns(Result<IConnection>.Fail("Failed to connect to RabbitMQ"));
        var reading = MotionReading();

        // Act
        var result = await _sut.PublishAsync(reading, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.ErrorMessage.ShouldBe("Failed to connect to RabbitMQ");
        A.CallTo(_connection).MustNotHaveHappened();
        RecordedPublishes().ShouldBe([("MotionReadingStored", PublishOutcome.Skipped)]);
        var warning = _logger.Collector.GetSnapshot().ShouldHaveSingleItem();
        warning.Level.ShouldBe(LogLevel.Warning);
        warning.Message.ShouldStartWith("Failed to publish MotionReadingStored ");
        warning.Message.ShouldEndWith($" for stored reading {reading.Id}. Reason: Failed to connect to RabbitMQ");
    }

    [Fact]
    public async Task PublishAsync_ConnectionUnavailableOnce_OpensChannelOnNextPublish()
    {
        // Arrange
        A.CallTo(() => _connectionProvider.GetConnectionAsync(A<CancellationToken>._))
            .Returns(Result<IConnection>.Fail("Failed to connect to RabbitMQ")).Once()
            .Then.Returns(Result<IConnection>.Succeed(_connection));
        await _sut.PublishAsync(MotionReading(), TestContext.Current.CancellationToken);

        // Act
        var result = await _sut.PublishAsync(MotionReading(), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        _channel.PublishedMessages().ShouldHaveSingleItem();
    }

    [Fact]
    public async Task PublishAsync_ChannelCannotBeOpened_ReturnsFailureAndLogsException()
    {
        // Arrange
        var exception = new InvalidOperationException("Channel limit reached");
        A.CallTo(() => _connection.CreateChannelAsync(A<CreateChannelOptions?>._, A<CancellationToken>._)).ThrowsAsync(exception);

        // Act
        var result = await _sut.PublishAsync(MotionReading(), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.ErrorMessage.ShouldBe("Failed to open RabbitMQ channel");
        RecordedPublishes().ShouldBe([("MotionReadingStored", PublishOutcome.Skipped)]);
        var error = _logger.Collector.GetSnapshot().Where(entry => entry.Level == LogLevel.Error).ShouldHaveSingleItem();
        error.Message.ShouldBe("Exception occurred while opening RabbitMQ channel");
        error.Exception.ShouldBeSameAs(exception);
    }

    [Fact]
    public async Task PublishAsync_ExchangeCannotBeDeclared_ReturnsFailureAndDisposesChannelWithoutPublishing()
    {
        // Arrange
        var exception = new InvalidOperationException("PRECONDITION_FAILED - inequivalent arg 'type'");
        _channel.CallsToExchangeDeclareAsync().ThrowsAsync(exception);

        // Act
        var result = await _sut.PublishAsync(MotionReading(), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.ErrorMessage.ShouldBe($"Failed to declare RabbitMQ exchange {ExchangeName}");
        _channel.CallsToBasicPublishAsync().MustNotHaveHappened();
        A.CallTo(() => _channel.DisposeAsync()).MustHaveHappenedOnceExactly();
        RecordedPublishes().ShouldBe([("MotionReadingStored", PublishOutcome.Skipped)]);
        var error = _logger.Collector.GetSnapshot().Where(entry => entry.Level == LogLevel.Error).ShouldHaveSingleItem();
        error.Message.ShouldBe($"Exception occurred while declaring RabbitMQ exchange {ExchangeName} of type topic");
        error.Exception.ShouldBeSameAs(exception);
    }

    [Fact]
    public async Task PublishAsync_ExchangeDeclarationFailedOnce_RetriesOnNextPublish()
    {
        // Arrange
        _channel.CallsToExchangeDeclareAsync()
            .ThrowsAsync(new InvalidOperationException("Broker unavailable")).Once()
            .Then.Returns(Task.CompletedTask);
        await _sut.PublishAsync(MotionReading(), TestContext.Current.CancellationToken);

        // Act
        var result = await _sut.PublishAsync(MotionReading(), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        _channel.CallsToExchangeDeclareAsync().MustHaveHappenedTwiceExactly();
        _channel.PublishedMessages().ShouldHaveSingleItem();
    }

    [Fact]
    public async Task PublishAsync_MessageNackedByBroker_ReturnsFailureLogsErrorAndRecordsNackedPublish()
    {
        // Arrange
        var exception = new PublishException(1, isReturn: false);
        _channel.CallsToBasicPublishAsync().ThrowsAsync(exception);

        // Act
        var result = await _sut.PublishAsync(EnergyReading(), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.ErrorMessage.ShouldBe("Message was rejected by RabbitMQ");
        var messageId = SinglePublishedMessageId();
        var error = _logger.Collector.GetSnapshot().Where(entry => entry.Level == LogLevel.Error).ShouldHaveSingleItem();
        error.Message.ShouldBe(
            $"EnergyReadingStored {messageId} was nacked by the broker. Exchange: {ExchangeName}. " +
            "Routing key: meter-reading.energy.stored");
        error.Exception.ShouldBeSameAs(exception);
        RecordedPublishes().ShouldBe([("EnergyReadingStored", PublishOutcome.Nacked)]);
    }

    [Fact]
    public async Task PublishAsync_PublishThrowsUnexpectedException_ReturnsFailureLogsErrorAndRecordsFailedPublish()
    {
        // Arrange
        var exception = new InvalidOperationException("Channel is broken");
        _channel.CallsToBasicPublishAsync().ThrowsAsync(exception);

        // Act
        var result = await _sut.PublishAsync(AirQualityReading(), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.ErrorMessage.ShouldBe("Failed to publish message to RabbitMQ");
        var messageId = SinglePublishedMessageId();
        var error = _logger.Collector.GetSnapshot().Where(entry => entry.Level == LogLevel.Error).ShouldHaveSingleItem();
        error.Message.ShouldBe(
            $"Exception occurred while publishing AirQualityReadingStored {messageId}. Exchange: {ExchangeName}. " +
            "Routing key: meter-reading.air-quality.stored");
        error.Exception.ShouldBeSameAs(exception);
        RecordedPublishes().ShouldBe([("AirQualityReadingStored", PublishOutcome.Failed)]);
    }

    [Fact]
    public async Task PublishAsync_PublishFails_LogsWarningWithReadingIdAndReason()
    {
        // Arrange
        _channel.CallsToBasicPublishAsync().ThrowsAsync(new PublishException(1, isReturn: false));
        var reading = EnergyReading();

        // Act
        await _sut.PublishAsync(reading, TestContext.Current.CancellationToken);

        // Assert
        var messageId = SinglePublishedMessageId();
        var warning = _logger.Collector.GetSnapshot().Where(entry => entry.Level == LogLevel.Warning).ShouldHaveSingleItem();
        warning.Message.ShouldBe(
            $"Failed to publish EnergyReadingStored {messageId} for stored reading {reading.Id}. " +
            "Reason: Message was rejected by RabbitMQ");
    }

    [Fact]
    public async Task PublishAsync_ConfirmNotReceivedWithinTimeout_ReturnsFailureLogsTimeoutAndRecordsTimedOutPublish()
    {
        // Arrange
        _channel.HangsUntilPublishIsCancelled();

        // Act
        var publishTask = _sut.PublishAsync(MotionReading(), TestContext.Current.CancellationToken);
        await TestWait.UntilAsync(() => _channel.PublishedMessages().Count == 1, TestContext.Current.CancellationToken);

        _timeProvider.Advance(TimeSpan.FromSeconds(PublishTimeoutInSeconds) - TimeSpan.FromMilliseconds(1));
        var completedBeforeTimeout = publishTask.IsCompleted;

        _timeProvider.Advance(TimeSpan.FromMilliseconds(1));
        var result = await publishTask;

        // Assert
        completedBeforeTimeout.ShouldBeFalse();
        result.IsFailure.ShouldBeTrue();
        result.ErrorMessage.ShouldBe("Message was not confirmed by RabbitMQ in time");
        var messageId = SinglePublishedMessageId();
        _logger.Collector.GetSnapshot().ShouldContain(entry =>
            entry.Level == LogLevel.Error &&
            entry.Message == $"MotionReadingStored {messageId} was not confirmed within {PublishTimeoutInSeconds} seconds. " +
                $"Exchange: {ExchangeName}. Routing key: meter-reading.motion.stored");
        RecordedPublishes().ShouldBe([("MotionReadingStored", PublishOutcome.TimedOut)]);
    }

    [Fact]
    public async Task PublishAsync_CallerCancelsWhilePublishIsInFlight_ThrowsOperationCanceledException()
    {
        // Arrange
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        _channel.CallsToBasicPublishAsync()
            .ReturnsLazily(call => new ValueTask(CancelAndWaitAsync(call.GetArgument<CancellationToken>(5))));

        async Task CancelAndWaitAsync(CancellationToken publishToken)
        {
            await cts.CancelAsync();

            await Task.Delay(Timeout.Infinite, publishToken);
        }

        // Act
        var act = () => _sut.PublishAsync(MotionReading(), cts.Token);

        // Assert
        await act.ShouldThrowAsync<OperationCanceledException>();
        _logger.Collector.GetSnapshot().ShouldNotContain(entry => entry.Level >= LogLevel.Warning);
        A.CallTo(_metrics).MustNotHaveHappened();
    }

    [Fact]
    public async Task PublishAsync_CallerCancelsWhileDeclaringExchange_ThrowsAndDisposesChannel()
    {
        // Arrange
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        _channel.CallsToExchangeDeclareAsync().ReturnsLazily(async () =>
        {
            await cts.CancelAsync();

            cts.Token.ThrowIfCancellationRequested();
        });

        // Act
        var act = () => _sut.PublishAsync(MotionReading(), cts.Token);

        // Assert
        await act.ShouldThrowAsync<OperationCanceledException>();
        A.CallTo(() => _channel.DisposeAsync()).MustHaveHappenedOnceExactly();
        _channel.CallsToBasicPublishAsync().MustNotHaveHappened();
    }

    [Fact]
    public async Task PublishAsync_AnyReading_PassesCancellationTokenToConnectionProvider()
    {
        // Arrange
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);

        // Act
        await _sut.PublishAsync(MotionReading(), cts.Token);

        // Assert
        A.CallTo(() => _connectionProvider.GetConnectionAsync(cts.Token)).MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task DisposeAsync_ChannelOpened_ClosesAndDisposesChannel()
    {
        // Arrange
        await _sut.PublishAsync(MotionReading(), TestContext.Current.CancellationToken);

        // Act
        await _sut.DisposeAsync();

        // Assert
        _channel.CallsToCloseAsync().MustHaveHappenedOnceExactly()
            .Then(A.CallTo(() => _channel.DisposeAsync()).MustHaveHappenedOnceExactly());
    }

    [Fact]
    public async Task DisposeAsync_CloseThrows_LogsWarningAndStillDisposesChannel()
    {
        // Arrange
        await _sut.PublishAsync(MotionReading(), TestContext.Current.CancellationToken);
        var exception = new InvalidOperationException("Connection is gone");
        _channel.CallsToCloseAsync().ThrowsAsync(exception);

        // Act
        await _sut.DisposeAsync();

        // Assert
        A.CallTo(() => _channel.DisposeAsync()).MustHaveHappenedOnceExactly();
        var warning = _logger.Collector.GetSnapshot().ShouldHaveSingleItem();
        warning.Level.ShouldBe(LogLevel.Warning);
        warning.Message.ShouldBe("Failed to close the publishing channel");
        warning.Exception.ShouldBeSameAs(exception);
    }

    [Fact]
    public async Task DisposeAsync_NothingPublished_CompletesWithoutConnecting()
    {
        // Act
        await _sut.DisposeAsync();

        // Assert
        A.CallTo(_connectionProvider).MustNotHaveHappened();
    }

    private static MeterReading MotionReading()
    {
        return CreateReading(MeterType.Motion, MeterLocation.Garage, new MotionPayload { MotionDetected = true });
    }

    private static MeterReading EnergyReading()
    {
        return CreateReading(MeterType.Energy, MeterLocation.Office, new EnergyPayload { Energy = 845.62 });
    }

    private static MeterReading AirQualityReading()
    {
        return CreateReading(
            MeterType.AirQuality,
            MeterLocation.LivingRoom,
            new AirQualityPayload { Co2 = 803, Pm25 = 25, Humidity = 36 });
    }

    private static MeterReading CreateReading(MeterType type, MeterLocation location, IReadingPayload payload)
    {
        return new MeterReading
        {
            Id = Guid.CreateVersion7(),
            DeduplicationKey = Guid.CreateVersion7(),
            Type = type,
            Location = location,
            ReceivedAt = ReceivedAt,
            Payload = payload
        };
    }

    private List<(string MessageType, PublishOutcome Outcome)> RecordedPublishes()
    {
        return
        [
            .. Fake.GetCalls(_metrics)
                .Where(call => call.Method.Name == nameof(IMetricsService.RecordPublish))
                .Select(call => (call.GetArgument<string>(0)!, call.GetArgument<PublishOutcome>(1)))
        ];
    }

    private string SinglePublishedMessageId()
    {
        return _channel.PublishedMessages().ShouldHaveSingleItem().Properties.MessageId!;
    }
}
