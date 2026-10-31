using Ausiannikau.Utilities.Results;
using DataProcessor.Application.Correlation;
using DataProcessor.Infrastructure.RabbitMq.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace DataProcessor.Infrastructure.RabbitMq.Connection;

internal interface IRabbitMqConnectionProvider
{
    public Task<Result<IConnection>> GetConnectionAsync(CancellationToken ct);
}

internal sealed class RabbitMqConnectionProvider(
    IConnectionFactory connectionFactory,
    IOptions<RabbitMqSettings> options,
    ILogger<RabbitMqConnectionProvider> logger)
    : IRabbitMqConnectionProvider, IAsyncDisposable
{
    private readonly RabbitMqSettings _settings = options.Value;
    private readonly SemaphoreSlim _connectionLock = new(1, 1);

    private IConnection? _connection;

    public async Task<Result<IConnection>> GetConnectionAsync(CancellationToken ct)
    {
        // Once created, the connection recovers from network failures automatically,
        // so it is reused even while it is temporarily closed.
        if (_connection is not null)
        {
            return Result<IConnection>.Succeed(_connection);
        }

        await _connectionLock.WaitAsync(ct);

        try
        {
            _connection ??= await CreateConnectionAsync(ct);

            return Result<IConnection>.Succeed(_connection);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.FailedToConnect(_settings.Host, _settings.Port, ex);

            return Result<IConnection>.Fail("Failed to connect to RabbitMQ");
        }
        finally
        {
            _connectionLock.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_connection is not null)
        {
            try
            {
                await _connection.CloseAsync();
            }
            catch (Exception ex)
            {
                logger.FailedToCloseConnection(ex);
            }

            await _connection.DisposeAsync();
        }

        _connectionLock.Dispose();
    }

    private async Task<IConnection> CreateConnectionAsync(CancellationToken ct)
    {
        var connection = await connectionFactory.CreateConnectionAsync(ct);

        connection.ConnectionShutdownAsync += OnConnectionShutdownAsync;
        connection.RecoverySucceededAsync += OnRecoverySucceededAsync;
        connection.ConnectionRecoveryErrorAsync += OnConnectionRecoveryErrorAsync;
        connection.ConnectionBlockedAsync += OnConnectionBlockedAsync;
        connection.ConnectionUnblockedAsync += OnConnectionUnblockedAsync;

        logger.Connected(_settings.Host, _settings.Port);

        return connection;
    }

    private Task OnConnectionShutdownAsync(object sender, ShutdownEventArgs args)
    {
        CorrelationContext.Reset();

        logger.ConnectionShutDown(args.Initiator, args.ReplyCode, args.ReplyText);

        return Task.CompletedTask;
    }

    private Task OnRecoverySucceededAsync(object sender, AsyncEventArgs args)
    {
        CorrelationContext.Reset();

        logger.ConnectionRecovered();

        return Task.CompletedTask;
    }

    private Task OnConnectionRecoveryErrorAsync(object sender, ConnectionRecoveryErrorEventArgs args)
    {
        CorrelationContext.Reset();

        logger.ConnectionRecoveryFailed(args.Exception);

        return Task.CompletedTask;
    }

    private Task OnConnectionBlockedAsync(object sender, ConnectionBlockedEventArgs args)
    {
        CorrelationContext.Reset();

        logger.ConnectionBlocked(args.Reason);

        return Task.CompletedTask;
    }

    private Task OnConnectionUnblockedAsync(object sender, AsyncEventArgs args)
    {
        CorrelationContext.Reset();

        logger.ConnectionUnblocked();

        return Task.CompletedTask;
    }
}

internal static partial class RabbitMqConnectionProviderLogs
{
    [LoggerMessage(Level = LogLevel.Information, Message = "Connected to RabbitMQ at {Host}:{Port}")]
    public static partial void Connected(this ILogger logger, string host, int port);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to connect to RabbitMQ at {Host}:{Port}")]
    public static partial void FailedToConnect(this ILogger logger, string host, int port, Exception ex);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "RabbitMQ connection was shut down. Initiator: {Initiator}. Reply code: {ReplyCode}. Reply text: {ReplyText}")]
    public static partial void ConnectionShutDown(this ILogger logger, ShutdownInitiator initiator, ushort replyCode, string replyText);

    [LoggerMessage(Level = LogLevel.Information, Message = "RabbitMQ connection was recovered")]
    public static partial void ConnectionRecovered(this ILogger logger);

    [LoggerMessage(Level = LogLevel.Error, Message = "RabbitMQ connection recovery attempt failed")]
    public static partial void ConnectionRecoveryFailed(this ILogger logger, Exception ex);

    [LoggerMessage(Level = LogLevel.Warning, Message = "RabbitMQ connection was blocked by the broker. Reason: {Reason}")]
    public static partial void ConnectionBlocked(this ILogger logger, string reason);

    [LoggerMessage(Level = LogLevel.Information, Message = "RabbitMQ connection was unblocked by the broker")]
    public static partial void ConnectionUnblocked(this ILogger logger);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to close RabbitMQ connection")]
    public static partial void FailedToCloseConnection(this ILogger logger, Exception ex);
}
