using System.Text.Json;
using Ausiannikau.Utilities.Results;
using DataIngestor.Contracts.Events;
using DataProcessor.Application.MeterReadings;
using DataProcessor.Application.MeterReadings.Models;
using DataProcessor.Application.Telemetry.Metrics;
using DataProcessor.Application.Telemetry.Traces;
using DataProcessor.Infrastructure.RabbitMq.Connection;
using DataProcessor.Infrastructure.RabbitMq.Topology;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;

namespace DataProcessor.Infrastructure.RabbitMq.Consumers;

internal abstract class MeterReadingsConsumer<TEvent>(
    IRabbitMqConnectionProvider connectionProvider,
    IServiceScopeFactory scopeFactory,
    TimeProvider timeProvider,
    IMetricsService metrics,
    ITracingService tracing,
    ILogger logger)
    : BaseRabbitMqConsumer(connectionProvider, timeProvider, metrics, tracing, logger)
    where TEvent : MeterReadingEvent
{
    protected override string MessageType => typeof(TEvent).Name;

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

        var mappingResult = ToModel(readingEvent);

        if (!mappingResult.TryGetSuccessValue(out var reading))
        {
            return mappingResult;
        }

        await using var scope = scopeFactory.CreateAsyncScope();

        var processingService = scope.ServiceProvider.GetRequiredService<IReadingsProcessingService>();

        return await processingService.ProcessAsync(reading, ct);
    }

    protected abstract Result<MeterReadingModel> ToModel(TEvent message);

    private Result<TEvent> ParseMessage(ReadOnlyMemory<byte> body)
    {
        try
        {
            var readingEvent = JsonSerializer.Deserialize<TEvent>(body.Span, JsonSerializerOptions.Web);

            return readingEvent is null
                ? Result<TEvent>.Fail($"Message body is not a {MessageType}")
                : Result<TEvent>.Succeed(readingEvent);
        }
        catch (JsonException ex)
        {
            Logger.FailedToParseMessage(MessageType, ex);

            return Result<TEvent>.Fail($"Message body is not a valid {MessageType}");
        }
    }
}

internal static partial class MeterReadingsConsumerLogs
{
    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to parse the message body as {MessageType}")]
    public static partial void FailedToParseMessage(this ILogger logger, string messageType, Exception exception);
}
