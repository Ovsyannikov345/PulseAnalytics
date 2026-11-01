using System.Text;
using Ausiannikau.Utilities.Results;
using DataProcessor.Infrastructure.RabbitMq.Connection;
using DataProcessor.Infrastructure.RabbitMq.Topology;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;

namespace DataProcessor.Infrastructure.RabbitMq.Consumers;

internal abstract class MeterReadingsConsumer(IRabbitMqConnectionProvider connectionProvider, ILogger logger)
    : BaseRabbitMqConsumer(connectionProvider, logger)
{
    protected abstract string RoutingKey { get; }

    protected override async Task<Result> SetupQueueAsync(IChannel channel, CancellationToken ct)
    {
        await channel.ExchangeDeclarePassiveAsync(RabbitMqTopology.Exchanges.MeterReadingsExchange.Name, ct);

        await channel.QueueDeclareAsync(
            queue: QueueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            cancellationToken: ct);

        await channel.QueueBindAsync(
            queue: QueueName,
            exchange: RabbitMqTopology.Exchanges.MeterReadingsExchange.Name,
            routingKey: RoutingKey,
            cancellationToken: ct);

        await channel.BasicQosAsync(
            prefetchSize: 0,
            prefetchCount: 10,
            global: false,
            cancellationToken: ct);

        return Result.Succeed();
    }

    protected override Task<Result> ProcessMessageAsync(ReceivedMessage message, CancellationToken ct)
    {
        if (Logger.IsEnabled(LogLevel.Information))
        {
            var info = message.BasicProperties;

            Logger.ReadingProcessed(
                info.AppId,
                info.ContentType,
                info.DeliveryMode,
                info.MessageId,
                info.CorrelationId,
                info.Type,
                info.Timestamp,
                Encoding.UTF8.GetString(message.Body.Span));
        }

        return Task.FromResult(Result.Succeed());
    }
}

internal static partial class MeterReadingsConsumerLogs
{
    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Processed a new reading. App ID: {AppId}. Content type: {ContentType}. Delivery mode: {Mode}. " +
            "Message ID: {Id}. Correlation ID: {CorrelationId}. Type: {Type}. Timestamp: {Timestamp}. Body: {Body}")]
    public static partial void ReadingProcessed(
        this ILogger logger,
        string? appId,
        string? contentType,
        DeliveryModes mode,
        string? id,
        string? correlationId,
        string? type,
        AmqpTimestamp timestamp,
        string body);
}
