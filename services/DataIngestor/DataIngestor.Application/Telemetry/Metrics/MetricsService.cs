using System.Diagnostics;
using System.Diagnostics.Metrics;
using DataIngestor.Application.Telemetry.Metrics.Enums;
using DataIngestor.Domain.Constants.Enums;
using DataIngestor.Domain.Entities;

namespace DataIngestor.Application.Telemetry.Metrics;

public interface IMetricsService
{
    public void RecordPoll(TimeSpan duration, PollOutcome outcome);

    public void RecordFetch(FetchOutcome outcome);

    public void RecordReadingsReceived(IEnumerable<MeterReading> readings);

    public void RecordPublish(string messageType, PublishOutcome outcome);
}

internal sealed class MetricsService : IMetricsService
{
    public const string PollDurationName = "data_ingestor.poll.duration";

    public const string FetchesName = "data_ingestor.fetches";

    public const string ReadingsReceivedName = "data_ingestor.readings.received";

    public const string MessagesPublishedName = "data_ingestor.messages.published";

    private static readonly double[] PollDurationBuckets = [0.1, 0.25, 0.5, 1, 2, 5, 10, 20, 30];

    private readonly Histogram<double> _pollDuration;

    private readonly Counter<long> _fetches;

    private readonly Counter<long> _readingsReceived;

    private readonly Counter<long> _messagesPublished;

    public MetricsService(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create(IngestorTelemetry.ScopeName);

        _pollDuration = meter.CreateHistogram(
            PollDurationName,
            unit: "s",
            description: "Duration of a meter polling job run.",
            advice: new InstrumentAdvice<double> { HistogramBucketBoundaries = PollDurationBuckets });

        _fetches = meter.CreateCounter<long>(
            FetchesName,
            unit: "{fetch}",
            description: "Number of attempts to fetch meter readings from WeakApp, by outcome.");

        _readingsReceived = meter.CreateCounter<long>(
            ReadingsReceivedName,
            unit: "{reading}",
            description: "Number of meter readings received from WeakApp, by reading type.");

        _messagesPublished = meter.CreateCounter<long>(
            MessagesPublishedName,
            unit: "{message}",
            description: "Number of messages handed to RabbitMQ, by message type and outcome.");
    }

    public void RecordPoll(TimeSpan duration, PollOutcome outcome)
    {
        _pollDuration.Record(
            duration.TotalSeconds,
            new KeyValuePair<string, object?>(IngestorTelemetry.Tags.Outcome, ToTagValue(outcome)));
    }

    public void RecordFetch(FetchOutcome outcome)
    {
        _fetches.Add(
            1,
            new KeyValuePair<string, object?>(IngestorTelemetry.Tags.Outcome, ToTagValue(outcome)));
    }

    public void RecordReadingsReceived(IEnumerable<MeterReading> readings)
    {
        foreach (var group in readings.GroupBy(reading => reading.Type))
        {
            _readingsReceived.Add(
                group.Count(),
                new KeyValuePair<string, object?>(IngestorTelemetry.Tags.ReadingType, ToTagValue(group.Key)));
        }
    }

    public void RecordPublish(string messageType, PublishOutcome outcome)
    {
        _messagesPublished.Add(
            1,
            new KeyValuePair<string, object?>(IngestorTelemetry.Tags.MessageType, messageType),
            new KeyValuePair<string, object?>(IngestorTelemetry.Tags.Outcome, ToTagValue(outcome)));
    }

    private static string ToTagValue(PollOutcome outcome)
    {
        return outcome switch
        {
            PollOutcome.Succeeded => "succeeded",
            PollOutcome.Failed => "failed",
            _ => throw new UnreachableException($"No tag value for poll outcome {outcome}")
        };
    }

    private static string ToTagValue(FetchOutcome outcome)
    {
        return outcome switch
        {
            FetchOutcome.Succeeded => "succeeded",
            FetchOutcome.Empty => "empty",
            FetchOutcome.Failed => "failed",
            _ => throw new UnreachableException($"No tag value for fetch outcome {outcome}")
        };
    }

    private static string ToTagValue(PublishOutcome outcome)
    {
        return outcome switch
        {
            PublishOutcome.Confirmed => "confirmed",
            PublishOutcome.TimedOut => "timed_out",
            PublishOutcome.Returned => "returned",
            PublishOutcome.Nacked => "nacked",
            PublishOutcome.Failed => "failed",
            PublishOutcome.Skipped => "skipped",
            _ => throw new UnreachableException($"No tag value for publish outcome {outcome}")
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
