using System.Text.Json;
using Ausiannikau.Utilities.Results;
using DataProcessor.Contracts.Events;
using Microsoft.Extensions.Logging;
using NotificationService.Application.LiveReadings;
using NotificationService.Application.LiveReadings.Models;
using NotificationService.Application.Telemetry.Metrics;
using NotificationService.Application.Telemetry.Traces;
using NotificationService.Infrastructure.RabbitMq.Connection;
using NotificationService.Infrastructure.RabbitMq.Topology;
using RabbitMQ.Client;

namespace NotificationService.Infrastructure.RabbitMq.Consumers;

internal abstract class StoredReadingsConsumer<TEvent>(
    IRabbitMqConnectionProvider connectionProvider,
    IReadingsBroadcastService broadcastService,
    ServiceInstance instance,
    TimeProvider timeProvider,
    IMetricsService metrics,
    ITracingService tracing,
    ILogger logger)
    : BaseRabbitMqConsumer(connectionProvider, timeProvider, metrics, tracing, logger)
    where TEvent : MeterReadingStoredEvent
{
    private const ushort PrefetchCount = 50;

    protected override string QueueName => RabbitMqTopology.QueueName(QueueNamePrefix, instance);

    protected override string MessageType => typeof(TEvent).Name;

    protected abstract string QueueNamePrefix { get; }

    protected abstract string RoutingKey { get; }

    protected override async Task<Result> SetupQueueAsync(IChannel channel, CancellationToken ct)
    {
        await channel.ExchangeDeclarePassiveAsync(RabbitMqTopology.Exchanges.StoredReadingsExchange.Name, ct);

        // Exclusive and auto-delete: the queue lives only as long as this instance's connection and consumer,
        // so a stopped instance leaves nothing behind and nothing piles up while no instance runs.
        await channel.QueueDeclareAsync(
            queue: QueueName,
            durable: false,
            exclusive: true,
            autoDelete: true,
            arguments: new Dictionary<string, object?>
            {
                [RabbitMqTopology.QueueArguments.MessageTtl] = RabbitMqTopology.QueueArguments.MessageTtlInMilliseconds,
            },
            cancellationToken: ct);

        await channel.QueueBindAsync(
            queue: QueueName,
            exchange: RabbitMqTopology.Exchanges.StoredReadingsExchange.Name,
            routingKey: RoutingKey,
            cancellationToken: ct);

        await channel.BasicQosAsync(
            prefetchSize: 0,
            prefetchCount: PrefetchCount,
            global: false,
            cancellationToken: ct);

        return Result.Succeed();
    }

    protected override async Task<Result> ProcessMessageAsync(ReceivedMessage message, CancellationToken ct)
    {
        var parsingResult = ParseMessage(message.Body);

        if (!parsingResult.TryGetSuccessValue(out var storedEvent))
        {
            return parsingResult;
        }

        var mappingResult = ToModel(storedEvent);

        if (!mappingResult.TryGetSuccessValue(out var reading))
        {
            return mappingResult;
        }

        await broadcastService.BroadcastAsync(reading, ct);

        return Result.Succeed();
    }

    protected abstract Result<LiveReadingModel> ToModel(TEvent message);

    private Result<TEvent> ParseMessage(ReadOnlyMemory<byte> body)
    {
        try
        {
            var storedEvent = JsonSerializer.Deserialize<TEvent>(body.Span, JsonSerializerOptions.Web);

            return storedEvent is null
                ? Result<TEvent>.Fail($"Message body is not a {MessageType}")
                : Result<TEvent>.Succeed(storedEvent);
        }
        catch (JsonException ex)
        {
            Logger.FailedToParseMessage(MessageType, ex);

            return Result<TEvent>.Fail($"Message body is not a valid {MessageType}");
        }
    }
}

internal static partial class StoredReadingsConsumerLogs
{
    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to parse the message body as {MessageType}")]
    public static partial void FailedToParseMessage(this ILogger logger, string messageType, Exception exception);
}
