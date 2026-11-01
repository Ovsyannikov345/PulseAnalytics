using System.Text.Json;
using Ausiannikau.Utilities.Results;
using DataIngestor.Contracts.Events;
using DataProcessor.Application.Readings;
using DataProcessor.Application.Readings.Models;
using DataProcessor.Infrastructure.RabbitMq.Connection;
using DataProcessor.Infrastructure.RabbitMq.Topology;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;

namespace DataProcessor.Infrastructure.RabbitMq.Consumers;

internal abstract class MeterReadingsConsumer<TEvent>(
    IRabbitMqConnectionProvider connectionProvider,
    IServiceScopeFactory scopeFactory,
    ILogger logger)
    : BaseRabbitMqConsumer(connectionProvider, logger)
    where TEvent : MeterReadingEvent
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

    protected override async Task<Result> ProcessMessageAsync(ReceivedMessage message, CancellationToken ct)
    {
        var parsingResult = ParseMessage(message.Body);

        if (!parsingResult.TryGetSuccessValue(out var readingEvent))
        {
            return parsingResult;
        }

        // Consumers are singletons, while the processing service and its DbContext live per message.
        await using var scope = scopeFactory.CreateAsyncScope();

        var processingService = scope.ServiceProvider.GetRequiredService<IReadingsProcessingService>();

        return await processingService.ProcessAsync(ToModel(readingEvent), ct);
    }

    protected abstract MeterReadingModel ToModel(TEvent message);

    private Result<TEvent> ParseMessage(ReadOnlyMemory<byte> body)
    {
        try
        {
            var readingEvent = JsonSerializer.Deserialize<TEvent>(body.Span, JsonSerializerOptions.Web);

            return readingEvent is null
                ? Result<TEvent>.Fail($"Message body is not a {typeof(TEvent).Name}")
                : Result<TEvent>.Succeed(readingEvent);
        }
        catch (JsonException ex)
        {
            Logger.FailedToParseMessage(typeof(TEvent).Name, ex);

            return Result<TEvent>.Fail($"Message body is not a valid {typeof(TEvent).Name}");
        }
    }
}

internal static partial class MeterReadingsConsumerLogs
{
    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to parse the message body as {MessageType}")]
    public static partial void FailedToParseMessage(this ILogger logger, string messageType, Exception exception);
}
