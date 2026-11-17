using Ausiannikau.Utilities.Results;
using DataProcessor.Application.Interfaces.Messaging;
using DataProcessor.Application.Telemetry.Metrics;
using DataProcessor.Application.Telemetry.Metrics.Enums;
using DataProcessor.Domain.Entities.MeterReadings;
using DataProcessor.Infrastructure.Abstractions;
using DataProcessor.Infrastructure.RabbitMq.Configuration;
using DataProcessor.Infrastructure.RabbitMq.Connection;
using DataProcessor.Infrastructure.RabbitMq.Mappers;
using DataProcessor.Infrastructure.RabbitMq.Topology;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace DataProcessor.Infrastructure.RabbitMq.Publishers;

/// <summary>
/// Publishes one stored event per stored reading over a single long-lived channel.
/// </summary>
/// <remarks>
/// The processor publishes one event per consumed message, so opening a channel per event would cost a broker
/// round trip each time. Publishes are serialized, because a channel must not be used concurrently.
/// </remarks>
internal sealed class StoredReadingsPublisher(
    [FromKeyedServices(RabbitMqConnectionKeys.Publishing)] IRabbitMqConnectionProvider connectionProvider,
    IOptions<RabbitMqSettings> options,
    TimeProvider timeProvider,
    IMetricsService metrics,
    ILogger<StoredReadingsPublisher> logger)
    : BaseRabbitMqPublisher(connectionProvider, timeProvider, options, metrics, logger), IStoredReadingsPublisher, IAsyncDisposable
{
    private static readonly TimeSpan ChannelCloseTimeout = TimeSpan.FromSeconds(5);

    private readonly SemaphoreSlim _publishLock = new(1, 1);

    private IChannel? _channel;

    public async Task<Result> PublishAsync(MeterReading reading, CancellationToken ct)
    {
        var message = StoredReadingMessageMapper.ToOutgoingMessage(reading);

        await _publishLock.WaitAsync(ct);

        try
        {
            var publishingResult = await PublishAsync(message, ct);

            if (publishingResult.IsFailure)
            {
                Logger.FailedToPublishStoredReading(message.MessageType, message.MessageId, reading.Id, publishingResult.ErrorMessage);
            }

            return publishingResult;
        }
        finally
        {
            _publishLock.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_channel is not null)
        {
            await CloseChannelSafelyAsync(_channel);

            _channel = null;
        }

        _publishLock.Dispose();
    }

    private async Task<Result> PublishAsync(OutgoingMessage message, CancellationToken ct)
    {
        var channelResult = await GetChannelAsync(ct);

        if (!channelResult.TryGetSuccessValue(out var channel))
        {
            Metrics.RecordPublish(message.MessageType, PublishOutcome.Skipped);

            return channelResult;
        }

        return await PublishAsync(channel, message, ct);
    }

    private async Task<Result<IChannel>> GetChannelAsync(CancellationToken ct)
    {
        if (_channel is { IsOpen: true })
        {
            return Result<IChannel>.Succeed(_channel);
        }

        // A channel closed by the broker (e.g. after a channel error) is not recovered, so it is replaced.
        if (_channel is not null)
        {
            await CloseChannelSafelyAsync(_channel);

            _channel = null;
        }

        var channelResult = await OpenChannelAsync(ct);

        if (!channelResult.TryGetSuccessValue(out var channel))
        {
            return channelResult;
        }

        Result declarationResult;

        try
        {
            declarationResult = await DeclareExchangeAsync(
                channel,
                RabbitMqTopology.Exchanges.StoredReadingsExchange.Name,
                RabbitMqTopology.Exchanges.StoredReadingsExchange.Type,
                ct);
        }
        catch (OperationCanceledException)
        {
            await CloseChannelSafelyAsync(channel);

            throw;
        }

        if (declarationResult.IsFailure)
        {
            await CloseChannelSafelyAsync(channel);

            return declarationResult.ToFailureOf<IChannel>();
        }

        _channel = channel;

        return Result<IChannel>.Succeed(channel);
    }

    private async Task CloseChannelSafelyAsync(IChannel channel)
    {
        try
        {
            using var cts = new CancellationTokenSource(ChannelCloseTimeout);

            await channel.CloseAsync(cts.Token);
        }
        catch (Exception ex)
        {
            Logger.FailedToClosePublishingChannel(ex);
        }
        finally
        {
            await channel.DisposeAsync();
        }
    }
}

internal static partial class StoredReadingsPublisherLogs
{
    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Failed to publish {MessageType} {MessageId} for stored reading {ReadingId}. Reason: {Reason}")]
    public static partial void FailedToPublishStoredReading(
        this ILogger logger,
        string messageType,
        Guid messageId,
        Guid readingId,
        string reason);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to close the publishing channel")]
    public static partial void FailedToClosePublishingChannel(this ILogger logger, Exception ex);
}
