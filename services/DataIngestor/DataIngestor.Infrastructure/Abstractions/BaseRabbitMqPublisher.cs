using System.Text.Json;
using Ausiannikau.Utilities.Results;
using DataIngestor.Infrastructure.RabbitMq.Configuration;
using DataIngestor.Infrastructure.RabbitMq.Connection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;

namespace DataIngestor.Infrastructure.Abstractions;

internal abstract class BaseRabbitMqPublisher(
    IRabbitMqConnectionProvider connectionProvider,
    TimeProvider timeProvider,
    IOptions<RabbitMqSettings> options,
    ILogger logger)
{
    private const string ApplicationId = "data-ingestor";

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private static readonly CreateChannelOptions ChannelOptions = new(
        publisherConfirmationsEnabled: true,
        publisherConfirmationTrackingEnabled: true);

    protected RabbitMqSettings Settings => options.Value;

    protected ILogger Logger => logger;

    protected async Task<Result<IChannel>> OpenChannelAsync(CancellationToken ct)
    {
        var connectionResult = await connectionProvider.GetConnectionAsync(ct);

        if (!connectionResult.TryGetSuccessValue(out var connection))
        {
            return connectionResult.ToFailureOf<IChannel>();
        }

        try
        {
            return Result<IChannel>.Succeed(await connection.CreateChannelAsync(ChannelOptions, ct));
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            Logger.FailedToOpenChannel(ex);

            return Result<IChannel>.Fail("Failed to open RabbitMQ channel");
        }
    }

    protected async Task<Result> DeclareExchangeAsync(IChannel channel, string exchange, string exchangeType, CancellationToken ct)
    {
        try
        {
            await channel.ExchangeDeclareAsync(exchange, exchangeType, durable: true, autoDelete: false, cancellationToken: ct);

            return Result.Succeed();
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            Logger.FailedToDeclareExchange(exchange, exchangeType, ex);

            return Result.Fail($"Failed to declare RabbitMQ exchange {exchange}");
        }
    }

    protected async Task<Result> PublishAsync(IChannel channel, OutgoingMessage message, CancellationToken ct)
    {
        try
        {
            var body = JsonSerializer.SerializeToUtf8Bytes(message.Body, message.Body.GetType(), SerializerOptions);

            var properties = new BasicProperties
            {
                AppId = ApplicationId,
                ContentType = "application/json",
                DeliveryMode = DeliveryModes.Persistent,
                MessageId = message.MessageId.ToString(),
                Type = message.MessageType,
                Timestamp = new AmqpTimestamp(timeProvider.GetUtcNow().ToUnixTimeSeconds())
            };

            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(Settings.PublishTimeoutInSeconds), timeProvider);
            using var publishCts = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);

            await channel.BasicPublishAsync(message.Exchange, message.RoutingKey, mandatory: true, properties, body, publishCts.Token);

            return Result.Succeed();
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            Logger.PublishTimedOut(message.MessageType, message.MessageId, message.Exchange, message.RoutingKey, Settings.PublishTimeoutInSeconds);

            return Result.Fail("Message was not confirmed by RabbitMQ in time");
        }
        catch (PublishReturnException ex)
        {
            Logger.MessageReturned(message.MessageType, message.MessageId, message.Exchange, message.RoutingKey, ex.ReplyCode, ex.ReplyText);

            return Result.Fail("Message was returned by RabbitMQ as unroutable");
        }
        catch (PublishException ex)
        {
            Logger.MessageNacked(message.MessageType, message.MessageId, message.Exchange, message.RoutingKey, ex);

            return Result.Fail("Message was rejected by RabbitMQ");
        }
        catch (Exception ex)
        {
            Logger.FailedToPublish(message.MessageType, message.MessageId, message.Exchange, message.RoutingKey, ex);

            return Result.Fail("Failed to publish message to RabbitMQ");
        }
    }
}

internal sealed record OutgoingMessage(string Exchange, string RoutingKey, Guid MessageId, object Body)
{
    public string MessageType => Body.GetType().Name;
}

internal static partial class BaseRabbitMqPublisherLogs
{
    [LoggerMessage(Level = LogLevel.Error, Message = "Exception occurred while opening RabbitMQ channel")]
    public static partial void FailedToOpenChannel(this ILogger logger, Exception ex);

    [LoggerMessage(
        Level = LogLevel.Error,
        Message = "Exception occurred while declaring RabbitMQ exchange {Exchange} of type {ExchangeType}")]
    public static partial void FailedToDeclareExchange(this ILogger logger, string exchange, string exchangeType, Exception ex);

    [LoggerMessage(
        Level = LogLevel.Error,
        Message = "{MessageType} {MessageId} was not confirmed within {TimeoutInSeconds} seconds. " +
            "Exchange: {Exchange}. Routing key: {RoutingKey}")]
    public static partial void PublishTimedOut(
        this ILogger logger,
        string messageType,
        Guid messageId,
        string exchange,
        string routingKey,
        double timeoutInSeconds);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "{MessageType} {MessageId} was returned as unroutable. Exchange: {Exchange}. Routing key: {RoutingKey}. " +
            "Reply code: {ReplyCode}. Reply text: {ReplyText}")]
    public static partial void MessageReturned(
        this ILogger logger,
        string messageType,
        Guid messageId,
        string exchange,
        string routingKey,
        ushort replyCode,
        string replyText);

    [LoggerMessage(
        Level = LogLevel.Error,
        Message = "{MessageType} {MessageId} was nacked by the broker. Exchange: {Exchange}. Routing key: {RoutingKey}")]
    public static partial void MessageNacked(
        this ILogger logger,
        string messageType,
        Guid messageId,
        string exchange,
        string routingKey,
        Exception ex);

    [LoggerMessage(
        Level = LogLevel.Error,
        Message = "Exception occurred while publishing {MessageType} {MessageId}. Exchange: {Exchange}. Routing key: {RoutingKey}")]
    public static partial void FailedToPublish(
        this ILogger logger,
        string messageType,
        Guid messageId,
        string exchange,
        string routingKey,
        Exception ex);
}
