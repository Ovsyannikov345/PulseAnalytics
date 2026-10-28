using Ausiannikau.Utilities.Results;
using DataIngestor.Application.Interfaces.Messaging;
using DataIngestor.Application.Telemetry.Metrics;
using DataIngestor.Application.Telemetry.Metrics.Enums;
using DataIngestor.Domain.Entities;
using DataIngestor.Infrastructure.Abstractions;
using DataIngestor.Infrastructure.RabbitMq.Configuration;
using DataIngestor.Infrastructure.RabbitMq.Connection;
using DataIngestor.Infrastructure.RabbitMq.Mappers;
using DataIngestor.Infrastructure.RabbitMq.Topology;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DataIngestor.Infrastructure.RabbitMq.Publishers;

internal sealed class MeterReadingsPublisher(
    IRabbitMqConnectionProvider connectionProvider,
    IOptions<RabbitMqSettings> options,
    TimeProvider timeProvider,
    IMetricsService metrics,
    ILogger<MeterReadingsPublisher> logger)
    : BaseRabbitMqPublisher(connectionProvider, timeProvider, options, metrics, logger), IMeterReadingsPublisher
{
    public async Task<Result> PublishAsync(IReadOnlyCollection<MeterReading> readings, CancellationToken ct)
    {
        var channelResult = await OpenChannelAsync(ct);

        if (!channelResult.TryGetSuccessValue(out var channel))
        {
            RecordSkipped(readings);

            return channelResult;
        }

        await using (channel)
        {
            var declarationResult = await DeclareExchangeAsync(
                channel,
                MeterReadingsTopology.ExchangeName,
                MeterReadingsTopology.ExchangeType,
                ct);

            if (declarationResult.IsFailure)
            {
                RecordSkipped(readings);

                return declarationResult;
            }

            var attemptedCount = 0;
            var failedCount = 0;

            foreach (var reading in readings)
            {
                if (channel.IsClosed)
                {
                    Logger.ChannelClosedDuringBatch(readings.Count - attemptedCount);

                    RecordSkipped(readings.Skip(attemptedCount));

                    break;
                }

                var message = MeterReadingMessageMapper.ToOutgoingMessage(reading);

                attemptedCount++;

                var publishingResult = await PublishAsync(channel, message, ct);

                if (publishingResult.IsFailure)
                {
                    Logger.FailedToPublishReading(message.MessageType, message.MessageId, reading.Name, publishingResult.ErrorMessage);

                    failedCount++;
                }
            }

            var notPublishedCount = failedCount + (readings.Count - attemptedCount);

            if (notPublishedCount > 0)
            {
                return Result.Fail($"Failed to publish {notPublishedCount} of {readings.Count} meter readings");
            }

            Logger.PublishedReadings(readings.Count);

            return Result.Succeed();
        }
    }

    private void RecordSkipped(IEnumerable<MeterReading> readings)
    {
        foreach (var reading in readings)
        {
            Metrics.RecordPublish(MeterReadingMessageMapper.GetMessageType(reading), PublishOutcome.Skipped);
        }
    }
}

internal static partial class MeterReadingsPublisherLogs
{
    [LoggerMessage(Level = LogLevel.Information, Message = "Published {Count} meter readings")]
    public static partial void PublishedReadings(this ILogger logger, int count);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Failed to publish {MessageType} {MessageId} for location {Location}. Reason: {Reason}")]
    public static partial void FailedToPublishReading(
        this ILogger logger,
        string messageType,
        Guid messageId,
        string location,
        string reason);

    [LoggerMessage(
        Level = LogLevel.Error,
        Message = "RabbitMQ channel was closed while publishing, {SkippedCount} remaining meter readings were not published")]
    public static partial void ChannelClosedDuringBatch(this ILogger logger, int skippedCount);
}
