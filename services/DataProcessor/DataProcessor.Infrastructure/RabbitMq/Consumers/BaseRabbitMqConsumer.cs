using Ausiannikau.Utilities.Results;
using DataProcessor.Application.Correlation;
using DataProcessor.Infrastructure.RabbitMq.Connection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace DataProcessor.Infrastructure.RabbitMq.Consumers;

internal abstract class BaseRabbitMqConsumer(
    IRabbitMqConnectionProvider connectionProvider,
    ILogger logger)
    : BackgroundService
{
    private static readonly TimeSpan RestartDelay = TimeSpan.FromSeconds(5);

    private static readonly TimeSpan ChannelCloseTimeout = TimeSpan.FromSeconds(5);

    protected ILogger Logger => logger;

    protected abstract string QueueName { get; }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await ConsumeAsync();

            await Task.Delay(RestartDelay, stoppingToken);
        }

        async Task ConsumeAsync()
        {
            IConnection connection;
            IChannel channel;

            try
            {
                var connectionResult = await connectionProvider.GetConnectionAsync(stoppingToken);

                if (!connectionResult.TryGetSuccessValue(out var openedConnection))
                {
                    Logger.FailedToStart(connectionResult.ErrorMessage);

                    return;
                }

                connection = openedConnection;
                channel = await connection.CreateChannelAsync(cancellationToken: stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                Logger.ShuttingDown();

                return;
            }
            catch (Exception ex)
            {
                Logger.FailedToStart(ex);

                return;
            }

            try
            {
                try
                {
                    var queueSetupResult = await SetupQueueAsync(channel, stoppingToken);

                    if (queueSetupResult.IsFailure)
                    {
                        Logger.FailedToStart(queueSetupResult.ErrorMessage);

                        return;
                    }
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    Logger.ShuttingDown();

                    return;
                }
                catch (Exception ex)
                {
                    Logger.FailedToStart(ex);

                    return;
                }

                try
                {
                    var consumerStopped = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);

                    var consumer = new AsyncEventingBasicConsumer(channel);
                    consumer.ReceivedAsync += async (_, args) =>
                    {
                        await ProcessMessageSafelyAsync(channel, args, stoppingToken);
                    };

                    consumer.ShutdownAsync += (_, args) =>
                    {
                        if (connection.IsOpen)
                        {
                            consumerStopped.TrySetResult(
                                $"Channel was closed. Reply code: {args.ReplyCode}. Reply text: {args.ReplyText}");
                        }

                        return Task.CompletedTask;
                    };

                    consumer.UnregisteredAsync += (_, _) =>
                    {
                        if (channel.IsOpen)
                        {
                            consumerStopped.TrySetResult("Consumer was cancelled by the broker");
                        }

                        return Task.CompletedTask;
                    };

                    await channel.BasicConsumeAsync(
                        queue: QueueName,
                        autoAck: false,
                        consumer: consumer,
                        cancellationToken: stoppingToken);

                    var stopReason = await consumerStopped.Task.WaitAsync(stoppingToken);

                    Logger.ConsumerStopped(QueueName, stopReason, RestartDelay.TotalSeconds);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    Logger.ShuttingDown();

                    return;
                }
                catch (Exception ex)
                {
                    Logger.ListeningError(ex);

                    return;
                }
            }
            finally
            {
                await DisposeChannelSafelyAsync(channel);
            }
        }
    }

    protected abstract Task<Result> SetupQueueAsync(IChannel channel, CancellationToken ct);

    protected abstract Task<Result> ProcessMessageAsync(ReceivedMessage message, CancellationToken ct);

    private async Task ProcessMessageSafelyAsync(
        IChannel channel,
        BasicDeliverEventArgs message,
        CancellationToken serviceStoppingToken)
    {
        if (message.BasicProperties.CorrelationId is { Length: > 0 } correlationString
            && Guid.TryParse(correlationString, out var correlationId))
        {
            CorrelationContext.CorrelationId = correlationId;
        }
        else
        {
            CorrelationContext.CorrelationId = Guid.CreateVersion7();
            Logger.FailedToRestoreCorrelationId(message.BasicProperties.CorrelationId);
        }

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(serviceStoppingToken, message.CancellationToken);
        var ct = cts.Token;

        try
        {
            var processingResult = await ProcessMessageAsync(
                new ReceivedMessage
                {
                    BasicProperties = message.BasicProperties,
                    Body = message.Body
                },
                ct);

            if (processingResult.IsFailure)
            {
                Logger.MessageProcessingFailed(
                    message.BasicProperties.MessageId,
                    message.BasicProperties.Type,
                    processingResult.ErrorMessage);

                await NackSafelyAsync(channel, message, requeue: false);

                return;
            }

            Logger.MessageProcessed(message.BasicProperties.MessageId, message.BasicProperties.Type);

            await AckSafelyAsync(channel, message);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            var reason = serviceStoppingToken.IsCancellationRequested
                ? "Consumer shutting down"
                : "Message processing cancelled";

            Logger.MessageProcessingFailed(message.BasicProperties.MessageId, message.BasicProperties.Type, reason);

            await NackSafelyAsync(channel, message, requeue: true);
        }
        catch (Exception ex)
        {
            Logger.MessageProcessingFailed(message.BasicProperties.MessageId, message.BasicProperties.Type, ex);

            await NackSafelyAsync(channel, message, requeue: false);
        }
    }

    private async Task AckSafelyAsync(IChannel channel, BasicDeliverEventArgs message)
    {
        try
        {
            await channel.BasicAckAsync(message.DeliveryTag, multiple: false, CancellationToken.None);

            Logger.MessageAcked(message.BasicProperties.MessageId, message.BasicProperties.Type);
        }
        catch (Exception ex)
        {
            Logger.FailedToAck(message.BasicProperties.MessageId, message.BasicProperties.Type, ex);
        }
    }

    private async Task NackSafelyAsync(IChannel channel, BasicDeliverEventArgs message, bool requeue)
    {
        try
        {
            await channel.BasicNackAsync(message.DeliveryTag, multiple: false, requeue, CancellationToken.None);

            Logger.MessageNacked(message.BasicProperties.MessageId, message.BasicProperties.Type, requeue);
        }
        catch (Exception ex)
        {
            Logger.FailedToNack(message.BasicProperties.MessageId, message.BasicProperties.Type, ex);
        }
    }

    private async Task DisposeChannelSafelyAsync(IChannel channel)
    {
        try
        {
            using var cts = new CancellationTokenSource(ChannelCloseTimeout);

            await channel.CloseAsync(cts.Token);
        }
        catch (Exception ex)
        {
            Logger.FailedToCloseChannel(ex);
        }
        finally
        {
            await channel.DisposeAsync();
        }
    }
}

internal sealed record ReceivedMessage
{
    /// <summary>
    /// Basic properties of the message
    /// </summary>
    public required IReadOnlyBasicProperties BasicProperties { get; init; }

    /// <summary>
    /// Body of the message
    /// </summary>
    /// <remarks>
    /// Valid only until ProcessMessageAsync completes. Copy it (e.g. ToArray()) if it has to outlive the call.
    /// </remarks>
    public required ReadOnlyMemory<byte> Body { get; init; }
}

internal static partial class BaseRabbitMqConsumerLogs
{
    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to start RabbitMQ consumer. {Reason}")]
    public static partial void FailedToStart(this ILogger logger, string reason);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to start RabbitMQ consumer")]
    public static partial void FailedToStart(this ILogger logger, Exception exception);

    [LoggerMessage(Level = LogLevel.Error, Message = "Exception occurred while listening to RabbitMQ messages")]
    public static partial void ListeningError(this ILogger logger, Exception exception);

    [LoggerMessage(Level = LogLevel.Information, Message = "RabbitMQ consumer is shutting down")]
    public static partial void ShuttingDown(this ILogger logger);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "RabbitMQ consumer of queue {QueueName} stopped. {Reason}. Restarting in {RestartDelayInSeconds} seconds")]
    public static partial void ConsumerStopped(this ILogger logger, string queueName, string reason, double restartDelayInSeconds);

    [LoggerMessage(Level = LogLevel.Debug, Message = "RabbitMQ message {MessageId} ({MessageType}) is processed")]
    public static partial void MessageProcessed(this ILogger logger, string? messageId, string? messageType);

    [LoggerMessage(Level = LogLevel.Debug, Message = "RabbitMQ message {MessageId} ({MessageType}) is acked")]
    public static partial void MessageAcked(this ILogger logger, string? messageId, string? messageType);

    [LoggerMessage(Level = LogLevel.Warning, Message = "RabbitMQ message {MessageId} ({MessageType}) processing failed. {Reason}")]
    public static partial void MessageProcessingFailed(this ILogger logger, string? messageId, string? messageType, string reason);

    [LoggerMessage(Level = LogLevel.Warning, Message = "RabbitMQ message {MessageId} ({MessageType}) processing failed")]
    public static partial void MessageProcessingFailed(this ILogger logger, string? messageId, string? messageType, Exception exception);

    [LoggerMessage(Level = LogLevel.Information, Message = "RabbitMQ message {MessageId} ({MessageType}) is nacked. Requeued: {IsRequeued}")]
    public static partial void MessageNacked(this ILogger logger, string? messageId, string? messageType, bool isRequeued);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to ack RabbitMQ message {MessageId} ({MessageType})")]
    public static partial void FailedToAck(this ILogger logger, string? messageId, string? messageType, Exception exception);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to nack RabbitMQ message {MessageId} ({MessageType})")]
    public static partial void FailedToNack(this ILogger logger, string? messageId, string? messageType, Exception exception);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Failed to restore correlation ID from RabbitMQ message. Received correlation ID: {ReceivedId}")]
    public static partial void FailedToRestoreCorrelationId(this ILogger logger, string? receivedId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to close the channel")]
    public static partial void FailedToCloseChannel(this ILogger logger, Exception exception);
}
