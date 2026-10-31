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

            throw;
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

            throw;
        }
        catch (Exception ex)
        {
            Logger.FailedToStart(ex);

            return;
        }

        var consumer = new AsyncEventingBasicConsumer(_channel);
        consumer.ReceivedAsync += async (_, args) =>
        {
            await ProcessMessageAsync(args);
        };

        await _channel.BasicConsumeAsync(
            queue: QueueName,
            autoAck: false,
            consumer: consumer,
            cancellationToken: stoppingToken
        );

        await Task.Delay(Timeout.Infinite, stoppingToken);
    }

    protected abstract Task<Result> SetupQueueAsync(IChannel channel, CancellationToken ct);

    protected abstract Task<Result> ProcessMessageAsync(BasicDeliverEventArgs message);

    private async Task<Result<IChannel>> OpenChannelAsync(CancellationToken ct)
    {
        var connectionResult = await connectionProvider.GetConnectionAsync(ct);

        if (!connectionResult.TryGetSuccessValue(out var connection))
        {
            return connectionResult.ToFailureOf<IChannel>();
        }

        return Result<IChannel>.Succeed(await connection.CreateChannelAsync(cancellationToken: ct));
    }
}

internal static partial class BaseRabbitMqPublisherLogs
{
    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to start RabbitMq consumer. {Reason}")]
    public static partial void FailedToStart(this ILogger logger, string reason);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to start RabbitMq consumer")]
    public static partial void FailedToStart(this ILogger logger, Exception exception);

    [LoggerMessage(Level = LogLevel.Information, Message = "RabbitMq consumer is shutting down")]
    public static partial void ShuttingDown(this ILogger logger);
}
