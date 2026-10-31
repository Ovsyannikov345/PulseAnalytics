using System.Data.Common;
using System.Threading.Channels;
using Ausiannikau.Utilities.Results;
using DataProcessor.Infrastructure.RabbitMq.Connection;
using DataProcessor.Infrastructure.RabbitMq.Topology;
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

    protected ILogger Logger = logger;

    protected abstract string QueueName { get; }

    private IChannel? _channel = null;

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_channel is not null)
        {
            await _channel.CloseAsync(cancellationToken);
            _channel.Dispose();
        }

        await base.StopAsync(cancellationToken);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await ConsumeAsync();

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
                    cancellationToken: stoppingToken
                );

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
        ArgumentNullException.ThrowIfNull(_channel);

        var cts = CancellationTokenSource.CreateLinkedTokenSource(serviceStoppingToken, message.CancellationToken);
        var ct = cts.Token;

        try
        {
            var processingResult = await ProcessMessageAsync(
                new ReceivedMessage
                {
                    BasicProperties = message.BasicProperties,
                    Body = message.Body
                },
                cts.Token);

            if (processingResult.IsFailure)
            {
                Logger.MessageProcessingFailed(processingResult.ErrorMessage);

                await _channel.BasicNackAsync(message.DeliveryTag, multiple: false, requeue: false, CancellationToken.None);

                Logger.MessageNacked(isRequeued: false);
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
}

internal sealed record ReceivedMessage
{
    public required IReadOnlyBasicProperties BasicProperties { get; init; }

    public required ReadOnlyMemory<byte> Body { get; init; }
}

internal static partial class BaseRabbitMqPublisherLogs
{
    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to start RabbitMq consumer. {Reason}")]
    public static partial void FailedToStart(this ILogger logger, string reason);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to start RabbitMq consumer")]
    public static partial void FailedToStart(this ILogger logger, Exception exception);

    [LoggerMessage(Level = LogLevel.Error, Message = "Exception occurred while listening to RabbitMq messages")]
    public static partial void ListeningError(this ILogger logger, Exception exception);

    [LoggerMessage(Level = LogLevel.Information, Message = "RabbitMq consumer is shutting down")]
    public static partial void ShuttingDown(this ILogger logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "RabbitMq message is processed")]
    public static partial void MessageProcessed(this ILogger logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "RabbitMq message is acked")]
    public static partial void MessageAcked(this ILogger logger);

    [LoggerMessage(Level = LogLevel.Warning, Message = "RabbitMq message processing failed. {Reason}")]
    public static partial void MessageProcessingFailed(this ILogger logger, string reason);

    [LoggerMessage(Level = LogLevel.Warning, Message = "RabbitMq message processing failed")]
    public static partial void MessageProcessingFailed(this ILogger logger, Exception exception);

    [LoggerMessage(Level = LogLevel.Information, Message = "RabbitMq message is nacked. Requeued: {IsRequeued}")]
    public static partial void MessageNacked(this ILogger logger, bool isRequeued);
}
