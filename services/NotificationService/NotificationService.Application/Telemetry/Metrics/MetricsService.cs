using System.Diagnostics;
using System.Diagnostics.Metrics;
using NotificationService.Application.LiveReadings.Enums;
using NotificationService.Application.Telemetry.Metrics.Enums;

namespace NotificationService.Application.Telemetry.Metrics;

public interface IMetricsService
{
    public void RecordMessageProcessed(string messageType, TimeSpan duration, ConsumeOutcome outcome);

    public void RecordBroadcast(MeterType meterType, TimeSpan delay);
}

internal sealed class MetricsService : IMetricsService
{
    public const string MessageProcessingDurationName = "notification_service.message.processing.duration";

    public const string ReadingsBroadcastName = "notification_service.readings.broadcast";

    public const string ReadingDelayName = "notification_service.reading.delay";

    private static readonly double[] MessageProcessingDurationBuckets = [0.0001, 0.00025, 0.0005, 0.001, 0.0025, 0.005, 0.01, 0.025, 0.05, 0.1, 0.25, 0.5, 1, 2.5, 5];

    private static readonly double[] ReadingDelayBuckets = [0.005, 0.01, 0.025, 0.05, 0.1, 0.25, 0.5, 1, 2.5, 5, 10, 30];

    private readonly Histogram<double> _messageProcessingDuration;

    private readonly Counter<long> _readingsBroadcast;

    private readonly Histogram<double> _readingDelay;

    public MetricsService(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create(NotificationServiceTelemetry.ScopeName);

        _messageProcessingDuration = meter.CreateHistogram(
            MessageProcessingDurationName,
            unit: "s",
            description: "Duration of processing a consumed RabbitMQ message, by message type and outcome.",
            advice: new InstrumentAdvice<double> { HistogramBucketBoundaries = MessageProcessingDurationBuckets });

        _readingsBroadcast = meter.CreateCounter<long>(
            ReadingsBroadcastName,
            unit: "{reading}",
            description: "Number of stored readings pushed to the connected clients, by reading type.");

        _readingDelay = meter.CreateHistogram(
            ReadingDelayName,
            unit: "s",
            description: "Time from the ingestor receiving a reading to its push to the clients, by reading type.",
            advice: new InstrumentAdvice<double> { HistogramBucketBoundaries = ReadingDelayBuckets });
    }

    public void RecordMessageProcessed(string messageType, TimeSpan duration, ConsumeOutcome outcome)
    {
        _messageProcessingDuration.Record(
            duration.TotalSeconds,
            new KeyValuePair<string, object?>(NotificationServiceTelemetry.Tags.MessageType, messageType),
            new KeyValuePair<string, object?>(NotificationServiceTelemetry.Tags.Outcome, ToTagValue(outcome)));
    }

    public void RecordBroadcast(MeterType meterType, TimeSpan delay)
    {
        var readingType = new KeyValuePair<string, object?>(NotificationServiceTelemetry.Tags.ReadingType, ToTagValue(meterType));

        _readingsBroadcast.Add(1, readingType);
        _readingDelay.Record(delay.TotalSeconds, readingType);
    }

    private static string ToTagValue(ConsumeOutcome outcome)
    {
        return outcome switch
        {
            ConsumeOutcome.Acked => "acked",
            ConsumeOutcome.Rejected => "rejected",
            ConsumeOutcome.Requeued => "requeued",
            _ => throw new UnreachableException($"No tag value for consume outcome {outcome}")
        };
    }

    private static string ToTagValue(MeterType meterType)
    {
        return meterType switch
        {
            MeterType.AirQuality => "air_quality",
            MeterType.Energy => "energy",
            MeterType.Motion => "motion",
            _ => throw new UnreachableException($"No tag value for meter type {meterType}")
        };
    }
}
