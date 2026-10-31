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

    protected ILogger Logger => logger;

    protected abstract string QueueName { get; }

    private IChannel? _channel;

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await base.StopAsync(cancellationToken);
        await DisposeChannelSafelyAsync(cancellationToken);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await ConsumeAsync();

            await DisposeChannelSafelyAsync(stoppingToken);
            await Task.Delay(RestartDelay, stoppingToken);
        }

        async Task ConsumeAsync()
        {
            try
            {
                var channelOpeningResult = await OpenChannelAsync(stoppingToken);

                if (channelOpeningResult.TryGetSuccessValue(out var channel))
                {
                    _channel = channel;
                }
                else
                {
                    Logger.FailedToStart(channelOpeningResult.ErrorMessage);

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
                var queueSetupResult = await SetupQueueAsync(_channel, stoppingToken);

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
                var consumer = new AsyncEventingBasicConsumer(_channel);
                consumer.ReceivedAsync += async (_, args) =>
                {
                    await ProcessMessageSafelyAsync(args, stoppingToken);
                };

                await _channel.BasicConsumeAsync(
                    queue: QueueName,
                    autoAck: false,
                    consumer: consumer,
                    cancellationToken: stoppingToken);

                await Task.Delay(Timeout.Infinite, stoppingToken);
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
    }

    protected abstract Task<Result> SetupQueueAsync(IChannel channel, CancellationToken ct);

    protected abstract Task<Result> ProcessMessageAsync(ReceivedMessage message, CancellationToken ct);

    private async Task<Result<IChannel>> OpenChannelAsync(CancellationToken ct)
    {
        var connectionResult = await connectionProvider.GetConnectionAsync(ct);

        if (!connectionResult.TryGetSuccessValue(out var connection))
        {
            return connectionResult.ToFailureOf<IChannel>();
        }

        return Result<IChannel>.Succeed(await connection.CreateChannelAsync(cancellationToken: ct));
    }

    private async Task ProcessMessageSafelyAsync(
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

        ArgumentNullException.ThrowIfNull(_channel);

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
                Logger.MessageProcessingFailed(processingResult.ErrorMessage);

                await _channel.BasicNackAsync(message.DeliveryTag, multiple: false, requeue: false, CancellationToken.None);

                Logger.MessageNacked(isRequeued: false);

                return;
            }

            Logger.MessageProcessed();

            await _channel.BasicAckAsync(message.DeliveryTag, multiple: false, CancellationToken.None);

            Logger.MessageAcked();
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            var reason = serviceStoppingToken.IsCancellationRequested
                ? "Consumer shutting down"
                : "Message processing cancelled";

            Logger.MessageProcessingFailed(reason);

            await _channel.BasicNackAsync(message.DeliveryTag, multiple: false, requeue: true, CancellationToken.None);

            Logger.MessageNacked(isRequeued: true);
        }
        catch (Exception ex)
        {
            Logger.MessageProcessingFailed(ex);

            await _channel.BasicNackAsync(message.DeliveryTag, multiple: false, requeue: false, CancellationToken.None);

            Logger.MessageNacked(isRequeued: false);
        }
    }

    private async Task DisposeChannelSafelyAsync(CancellationToken ct)
    {
        if (_channel is not null)
        {
            try
            {
                await _channel.CloseAsync(ct);
                await _channel.DisposeAsync();
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Failed to dispose the connection");
            }
        }
    }
}

internal sealed record ReceivedMessage
{
    public required IReadOnlyBasicProperties BasicProperties { get; init; }

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

    [LoggerMessage(Level = LogLevel.Information, Message = "RabbitMQ message {MessageId} ({MessageType}) is processed")]
    public static partial void MessageProcessed(this ILogger logger, string messageId, string messageType);

    [LoggerMessage(Level = LogLevel.Information, Message = "RabbitMQ message {MessageId} ({MessageType}) is acked")]
    public static partial void MessageAcked(this ILogger logger, string messageId, string messageType);

    [LoggerMessage(Level = LogLevel.Warning, Message = "RabbitMQ message {MessageId} ({MessageType}) processing failed. {Reason}")]
    public static partial void MessageProcessingFailed(this ILogger logger, string messageId, string messageType, string reason);

    [LoggerMessage(Level = LogLevel.Warning, Message = "RabbitMQ message {MessageId} ({MessageType}) processing failed")]
    public static partial void MessageProcessingFailed(this ILogger logger, string messageId, string messageType, Exception exception);

    [LoggerMessage(Level = LogLevel.Information, Message = "RabbitMQ message {MessageId} ({MessageType}) is nacked. Requeued: {IsRequeued}")]
    public static partial void MessageNacked(this ILogger logger, bool isRequeued, string messageId, string messageType);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Failed to restore correlation ID from RabbitMQ message. Received correlation ID: {ReceivedId}")]
    public static partial void FailedToRestoreCorrelationId(this ILogger logger, string? receivedId);
}
