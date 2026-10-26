using DataIngestor.Application.Correlation;
using DataIngestor.Infrastructure.RabbitMq.Configuration;
using DataIngestor.Infrastructure.RabbitMq.Connection;
using FakeItEasy;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Exceptions;
using Shouldly;
using Xunit;

namespace DataIngestor.UnitTests.Infrastructure.RabbitMq.Connection;

public sealed class RabbitMqConnectionProviderTests
{
    private readonly IConnectionFactory _connectionFactory = A.Fake<IConnectionFactory>();
    private readonly IConnection _connection = A.Fake<IConnection>();
    private readonly FakeLogger<RabbitMqConnectionProvider> _logger = new();
    private readonly RabbitMqConnectionProvider _sut;

    public RabbitMqConnectionProviderTests()
    {
        A.CallTo(() => _connectionFactory.CreateConnectionAsync(A<CancellationToken>._))
            .Returns(_connection);

        var settings = new RabbitMqSettings
        {
            Host = "rabbitmq.test",
            Port = 5672,
            VirtualHost = "/",
            UserName = "ingestor",
            Password = "secret",
            ClientProvidedName = "pulse-analytics.data-ingestor",
            PublishTimeoutInSeconds = 5
        };

        _sut = new RabbitMqConnectionProvider(_connectionFactory, Options.Create(settings), _logger);
    }

    [Fact]
    public async Task GetConnectionAsync_FirstCall_ReturnsConnectionCreatedByFactory()
    {
        // Act
        var result = await _sut.GetConnectionAsync(TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBeSameAs(_connection);
        _logger.Collector.GetSnapshot().ShouldContain(entry =>
            entry.Level == LogLevel.Information && entry.Message == "Connected to RabbitMQ at rabbitmq.test:5672");
    }

    [Fact]
    public async Task GetConnectionAsync_CalledRepeatedly_CreatesConnectionOnce()
    {
        // Act
        await _sut.GetConnectionAsync(TestContext.Current.CancellationToken);
        var result = await _sut.GetConnectionAsync(TestContext.Current.CancellationToken);

        // Assert
        result.Value.ShouldBeSameAs(_connection);
        A.CallTo(() => _connectionFactory.CreateConnectionAsync(A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task GetConnectionAsync_FactoryThrows_ReturnsFailureAndLogsError()
    {
        // Arrange
        var exception = new BrokerUnreachableException(new IOException("Connection refused"));
        A.CallTo(() => _connectionFactory.CreateConnectionAsync(A<CancellationToken>._))
            .ThrowsAsync(exception);

        // Act
        var result = await _sut.GetConnectionAsync(TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeFalse();
        var error = _logger.Collector.GetSnapshot().Where(entry => entry.Level == LogLevel.Error).ShouldHaveSingleItem();
        error.Message.ShouldBe("Failed to connect to RabbitMQ at rabbitmq.test:5672");
        error.Exception.ShouldBeSameAs(exception);
    }

    [Fact]
    public async Task GetConnectionAsync_PreviousAttemptFailed_RetriesConnecting()
    {
        // Arrange
        A.CallTo(() => _connectionFactory.CreateConnectionAsync(A<CancellationToken>._))
            .ThrowsAsync(new BrokerUnreachableException(new IOException("Connection refused"))).Once()
            .Then.Returns(_connection);

        await _sut.GetConnectionAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _sut.GetConnectionAsync(TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBeSameAs(_connection);
    }

    [Fact]
    public async Task GetConnectionAsync_CancelledWhileConnecting_ThrowsWithoutLoggingError()
    {
        // Arrange
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        A.CallTo(() => _connectionFactory.CreateConnectionAsync(A<CancellationToken>._))
            .Invokes(cts.Cancel)
            .ThrowsAsync(new OperationCanceledException(cts.Token));

        // Act & Assert
        await Should.ThrowAsync<OperationCanceledException>(() => _sut.GetConnectionAsync(cts.Token));
        _logger.Collector.GetSnapshot().ShouldNotContain(entry => entry.Level >= LogLevel.Warning);
    }

    [Fact]
    public async Task ConnectionShutdown_Raised_LogsWarningAndResetsCorrelationId()
    {
        // Arrange
        await _sut.GetConnectionAsync(TestContext.Current.CancellationToken);
        var correlationId = Guid.CreateVersion7();
        CorrelationContext.CorrelationId = correlationId;

        // Act
        _connection.ConnectionShutdownAsync += Raise.FreeForm<AsyncEventHandler<ShutdownEventArgs>>.With(
            _connection,
            new ShutdownEventArgs(ShutdownInitiator.Peer, 320, "CONNECTION_FORCED"));

        // Assert
        _logger.Collector.LatestRecord.Level.ShouldBe(LogLevel.Warning);
        _logger.Collector.LatestRecord.Message.ShouldBe(
            "RabbitMQ connection was shut down. Initiator: Peer. Reply code: 320. Reply text: CONNECTION_FORCED");
        CorrelationContext.CorrelationId.ShouldNotBe(correlationId);
    }

    [Fact]
    public async Task RecoverySucceeded_Raised_LogsInformationAndResetsCorrelationId()
    {
        // Arrange
        await _sut.GetConnectionAsync(TestContext.Current.CancellationToken);
        var correlationId = Guid.CreateVersion7();
        CorrelationContext.CorrelationId = correlationId;

        // Act
        _connection.RecoverySucceededAsync += Raise.FreeForm<AsyncEventHandler<AsyncEventArgs>>.With(
            _connection,
            AsyncEventArgs.Empty);

        // Assert
        _logger.Collector.LatestRecord.Level.ShouldBe(LogLevel.Information);
        _logger.Collector.LatestRecord.Message.ShouldBe("RabbitMQ connection was recovered");
        CorrelationContext.CorrelationId.ShouldNotBe(correlationId);
    }

    [Fact]
    public async Task ConnectionRecoveryError_Raised_LogsErrorAndResetsCorrelationId()
    {
        // Arrange
        await _sut.GetConnectionAsync(TestContext.Current.CancellationToken);
        var correlationId = Guid.CreateVersion7();
        CorrelationContext.CorrelationId = correlationId;
        var exception = new IOException("Connection refused");

        // Act
        _connection.ConnectionRecoveryErrorAsync += Raise.FreeForm<AsyncEventHandler<ConnectionRecoveryErrorEventArgs>>.With(
            _connection,
            new ConnectionRecoveryErrorEventArgs(exception));

        // Assert
        _logger.Collector.LatestRecord.Level.ShouldBe(LogLevel.Error);
        _logger.Collector.LatestRecord.Message.ShouldBe("RabbitMQ connection recovery attempt failed");
        _logger.Collector.LatestRecord.Exception.ShouldBeSameAs(exception);
        CorrelationContext.CorrelationId.ShouldNotBe(correlationId);
    }

    [Fact]
    public async Task ConnectionBlocked_Raised_LogsWarningAndResetsCorrelationId()
    {
        // Arrange
        await _sut.GetConnectionAsync(TestContext.Current.CancellationToken);
        var correlationId = Guid.CreateVersion7();
        CorrelationContext.CorrelationId = correlationId;

        // Act
        _connection.ConnectionBlockedAsync += Raise.FreeForm<AsyncEventHandler<ConnectionBlockedEventArgs>>.With(
            _connection,
            new ConnectionBlockedEventArgs("low on memory"));

        // Assert
        _logger.Collector.LatestRecord.Level.ShouldBe(LogLevel.Warning);
        _logger.Collector.LatestRecord.Message.ShouldBe("RabbitMQ connection was blocked by the broker. Reason: low on memory");
        CorrelationContext.CorrelationId.ShouldNotBe(correlationId);
    }

    [Fact]
    public async Task ConnectionUnblocked_Raised_LogsInformationAndResetsCorrelationId()
    {
        // Arrange
        await _sut.GetConnectionAsync(TestContext.Current.CancellationToken);
        var correlationId = Guid.CreateVersion7();
        CorrelationContext.CorrelationId = correlationId;

        // Act
        _connection.ConnectionUnblockedAsync += Raise.FreeForm<AsyncEventHandler<AsyncEventArgs>>.With(
            _connection,
            AsyncEventArgs.Empty);

        // Assert
        _logger.Collector.LatestRecord.Level.ShouldBe(LogLevel.Information);
        _logger.Collector.LatestRecord.Message.ShouldBe("RabbitMQ connection was unblocked by the broker");
        CorrelationContext.CorrelationId.ShouldNotBe(correlationId);
    }

    [Fact]
    public async Task DisposeAsync_ConnectionCreated_ClosesAndDisposesConnection()
    {
        // Arrange
        await _sut.GetConnectionAsync(TestContext.Current.CancellationToken);

        // Act
        await _sut.DisposeAsync();

        // Assert
        A.CallTo(() => _connection.CloseAsync(A<ushort>._, A<string>._, A<TimeSpan>._, A<bool>._, A<CancellationToken>._))
            .MustHaveHappenedOnceExactly()
            .Then(A.CallTo(() => _connection.DisposeAsync()).MustHaveHappenedOnceExactly());
    }

    [Fact]
    public async Task DisposeAsync_CloseThrows_LogsWarningAndStillDisposesConnection()
    {
        // Arrange
        await _sut.GetConnectionAsync(TestContext.Current.CancellationToken);
        var exception = new AlreadyClosedException(new ShutdownEventArgs(ShutdownInitiator.Peer, 320, "CONNECTION_FORCED"));
        A.CallTo(() => _connection.CloseAsync(A<ushort>._, A<string>._, A<TimeSpan>._, A<bool>._, A<CancellationToken>._))
            .ThrowsAsync(exception);

        // Act
        await _sut.DisposeAsync();

        // Assert
        _logger.Collector.LatestRecord.Level.ShouldBe(LogLevel.Warning);
        _logger.Collector.LatestRecord.Message.ShouldBe("Failed to close RabbitMQ connection");
        _logger.Collector.LatestRecord.Exception.ShouldBeSameAs(exception);
        A.CallTo(() => _connection.DisposeAsync()).MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task DisposeAsync_NoConnectionCreated_CompletesWithoutConnecting()
    {
        // Act
        await _sut.DisposeAsync();

        // Assert
        A.CallTo(_connectionFactory).MustNotHaveHappened();
    }
}
