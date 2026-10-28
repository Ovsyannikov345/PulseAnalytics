using Ausiannikau.Utilities.Results;
using DataIngestor.Application.Correlation;
using DataIngestor.Application.ExternalMeters;
using DataIngestor.Application.Telemetry.Metrics;
using DataIngestor.Application.Telemetry.Metrics.Enums;
using DataIngestor.Application.Telemetry.Traces;
using DataIngestor.Background.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DataIngestor.Background.Workers;

internal sealed class MeterPollingWorker(
    IServiceScopeFactory scopeFactory,
    IOptions<MeterPollingWorkerSettings> options,
    TimeProvider timeProvider,
    IMetricsService metrics,
    ITracingService tracing,
    ILogger<MeterPollingWorker> logger)
    : BackgroundService
{
    private readonly MeterPollingWorkerSettings _settings = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (_settings.StartupDelayInSeconds is { } delayInSeconds)
        {
            logger.JobStartupDelayed(delayInSeconds);

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(delayInSeconds), timeProvider, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                logger.JobShuttingDown();

                throw;
            }
        }

        var pollingPeriod = TimeSpan.FromSeconds(_settings.PollingPeriodInSeconds);

        using var timer = new PeriodicTimer(pollingPeriod, timeProvider);

        logger.JobStarting(_settings.PollingPeriodInSeconds);

        try
        {
            do
            {
                await PollAsync(stoppingToken);
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            logger.JobShuttingDown();

            throw;
        }
    }

    private async Task PollAsync(CancellationToken stoppingToken)
    {
        CorrelationContext.CorrelationId = Guid.CreateVersion7(timeProvider.GetUtcNow());

        using var activity = tracing.StartPoll(CorrelationContext.CorrelationId);

        var startedAt = timeProvider.GetTimestamp();

        try
        {
            await using var serviceScope = scopeFactory.CreateAsyncScope();

            var result = await DoWorkAsync(serviceScope, stoppingToken);

            if (result.IsFailure)
            {
                logger.JobFailed(result.ErrorMessage);

                activity.MarkFailed(result.ErrorMessage);
            }

            metrics.RecordPoll(
                timeProvider.GetElapsedTime(startedAt),
                result.IsSuccess ? PollOutcome.Succeeded : PollOutcome.Failed);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.JobFailed(ex);

            activity.MarkFailed(ex);

            metrics.RecordPoll(timeProvider.GetElapsedTime(startedAt), PollOutcome.Failed);
        }
        finally
        {
            CorrelationContext.Reset();
        }
    }

    private static async Task<Result> DoWorkAsync(AsyncServiceScope serviceScope, CancellationToken ct)
    {
        var metersService = serviceScope.ServiceProvider.GetRequiredService<IExternalMetersService>();

        return await metersService.IngestReadingsAsync(ct);
    }
}

internal static partial class MeterPollingWorkerLogs
{
    [LoggerMessage(Level = LogLevel.Information, Message = "Meter polling job is waiting {DelayInSeconds} seconds before first attempt")]
    public static partial void JobStartupDelayed(this ILogger logger, int delayInSeconds);

    [LoggerMessage(Level = LogLevel.Information, Message = "Meter polling job is starting. Polling period: {PollingPeriodInSeconds} seconds")]
    public static partial void JobStarting(this ILogger logger, int pollingPeriodInSeconds);

    [LoggerMessage(Level = LogLevel.Error, Message = "Meter polling job failed. Reason: {Reason}")]
    public static partial void JobFailed(this ILogger logger, string reason);

    [LoggerMessage(Level = LogLevel.Error, Message = "Meter polling job failed")]
    public static partial void JobFailed(this ILogger logger, Exception exception);

    [LoggerMessage(Level = LogLevel.Information, Message = "Meter polling job is gracefully shutting down")]
    public static partial void JobShuttingDown(this ILogger logger);
}
