using Microsoft.Extensions.Logging;
using NotificationService.Application.LiveReadings.Enums;
using NotificationService.Application.LiveReadings.Models;
using NotificationService.Application.Telemetry.Metrics;

namespace NotificationService.Application.LiveReadings;

public interface IReadingsBroadcastService
{
    public Task BroadcastAsync(LiveReadingModel reading, CancellationToken ct);
}

internal sealed class ReadingsBroadcastService(
    IReadingsNotifier notifier,
    TimeProvider timeProvider,
    IMetricsService metrics,
    ILogger<ReadingsBroadcastService> logger)
    : IReadingsBroadcastService
{
    public async Task BroadcastAsync(LiveReadingModel reading, CancellationToken ct)
    {
        await notifier.NotifyReadingStoredAsync(reading, ct);

        // Clocks of different hosts can disagree slightly; a reading is never pushed before it was received.
        var delay = TimeSpan.FromTicks(Math.Max(0, (timeProvider.GetUtcNow() - reading.ReceivedAt).Ticks));

        metrics.RecordBroadcast(reading.Type, delay);

        logger.ReadingBroadcast(reading.Id, reading.Type, reading.Location, delay.TotalSeconds);
    }
}

internal static partial class ReadingsBroadcastServiceLogs
{
    [LoggerMessage(
        Level = LogLevel.Debug,
        Message = "Reading {ReadingId} ({ReadingType}, {Location}) is broadcast {DelayInSeconds} seconds after it was received")]
    public static partial void ReadingBroadcast(
        this ILogger logger,
        Guid readingId,
        MeterType readingType,
        MeterLocation location,
        double delayInSeconds);
}
