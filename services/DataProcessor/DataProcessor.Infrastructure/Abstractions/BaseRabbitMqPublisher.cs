using System.Text.Json;
using Ausiannikau.Utilities.Results;
using DataProcessor.Application.Correlation;
using DataProcessor.Application.Telemetry;
using DataProcessor.Application.Telemetry.Metrics;
using DataProcessor.Application.Telemetry.Metrics.Enums;
using DataProcessor.Infrastructure.RabbitMq.Configuration;
using DataProcessor.Infrastructure.RabbitMq.Connection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;

namespace DataProcessor.Infrastructure.Abstractions;

internal abstract class BaseRabbitMqPublisher(
    IRabbitMqConnectionProvider connectionProvider,
    TimeProvider timeProvider,
    IOptions<RabbitMqSettings> options,
    IMetricsService metrics,
    ILogger logger)
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private static readonly CreateChannelOptions ChannelOptions = new(
        publisherConfirmationsEnabled: true,
        publisherConfirmationTrackingEnabled: true);

    protected RabbitMqSettings Settings => options.Value;

    protected ILogger Logger => logger;

    protected IMetricsService Metrics => metrics;

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
                AppId = ProcessorTelemetry.ServiceName,
                ContentType = "application/json",
                DeliveryMode = message.Persistent ? DeliveryModes.Persistent : DeliveryModes.Transient,
                MessageId = message.MessageId.ToString(),
                CorrelationId = CorrelationContext.CorrelationId.ToString(),
                Type = message.MessageType,
                Timestamp = new AmqpTimestamp(timeProvider.GetUtcNow().ToUnixTimeSeconds())
            };

            using var timeoutCts = new CancellationTokenSource(
                TimeSpan.FromSeconds(Settings.PublishTimeoutInSeconds),
                timeProvider);

            using var publishCts = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);

            await channel.BasicPublishAsync(
                message.Exchange,
                message.RoutingKey,
                message.Mandatory,
                properties,
                body,
                publishCts.Token);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            Logger.PublishTimedOut(
                message.MessageType,
                message.MessageId,
                message.Exchange,
                message.RoutingKey,
                Settings.PublishTimeoutInSeconds);

            Metrics.RecordPublish(message.MessageType, PublishOutcome.TimedOut);

            return Result.Fail("Message was not confirmed by RabbitMQ in time");
        }
        catch (PublishException ex)
        {
            Logger.MessageNacked(message.MessageType, message.MessageId, message.Exchange, message.RoutingKey, ex);

            Metrics.RecordPublish(message.MessageType, PublishOutcome.Nacked);

            return Result.Fail("Message was rejected by RabbitMQ");
        }
        catch (Exception ex)
        {
            Logger.FailedToPublish(message.MessageType, message.MessageId, message.Exchange, message.RoutingKey, ex);

            Metrics.RecordPublish(message.MessageType, PublishOutcome.Failed);

            return Result.Fail("Failed to publish message to RabbitMQ");
        }

        Metrics.RecordPublish(message.MessageType, PublishOutcome.Confirmed);

        return Result.Succeed();
    }
}

/// <summary>
/// A message to publish.
/// </summary>
/// <param name="Mandatory">
/// Whether the broker has to route the message to at least one queue. Only <c>false</c> is used for now:
/// the publisher has no handling (log, outcome) for a returned message.
/// </param>
/// <param name="Persistent">Whether the broker writes the message to disk.</param>
internal sealed record OutgoingMessage(
    string Exchange,
    string RoutingKey,
    Guid MessageId,
    object Body,
    bool Mandatory,
    bool Persistent)
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
