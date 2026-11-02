using System.Diagnostics;
using System.Diagnostics.Metrics;
using DataProcessor.Application.Telemetry.Metrics.Enums;
using DataProcessor.Domain.Constants.Enums;

namespace DataProcessor.Application.Telemetry.Metrics;

public interface IMetricsService
{
    public void RecordMessageProcessed(string messageType, TimeSpan duration, ConsumeOutcome outcome);

    public void RecordReading(MeterType meterType, MeterLocation location, ReadingOutcome outcome);
}

internal sealed class MetricsService : IMetricsService
{
    public const string MessageProcessingDurationName = "data_processor.message.processing.duration";

    public const string ReadingsProcessedName = "data_processor.readings.processed";

    private static readonly double[] MessageProcessingDurationBuckets = [0.005, 0.01, 0.025, 0.05, 0.1, 0.25, 0.5, 1, 2.5, 5];

    private readonly Histogram<double> _messageProcessingDuration;

    private readonly Counter<long> _readingsProcessed;

    public MetricsService(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create(ProcessorTelemetry.ScopeName);

        _messageProcessingDuration = meter.CreateHistogram(
            MessageProcessingDurationName,
            unit: "s",
            description: "Duration of processing a consumed RabbitMQ message, by message type and outcome.",
            advice: new InstrumentAdvice<double> { HistogramBucketBoundaries = MessageProcessingDurationBuckets });

        _readingsProcessed = meter.CreateCounter<long>(
            ReadingsProcessedName,
            unit: "{reading}",
            description: "Number of meter readings processed, by reading type, location and outcome.");
    }

    public void RecordMessageProcessed(string messageType, TimeSpan duration, ConsumeOutcome outcome)
    {
        _messageProcessingDuration.Record(
            duration.TotalSeconds,
            new KeyValuePair<string, object?>(ProcessorTelemetry.Tags.MessageType, messageType),
            new KeyValuePair<string, object?>(ProcessorTelemetry.Tags.Outcome, ToTagValue(outcome)));
    }

    public void RecordReading(MeterType meterType, MeterLocation location, ReadingOutcome outcome)
    {
        _readingsProcessed.Add(
            1,
            new KeyValuePair<string, object?>(ProcessorTelemetry.Tags.ReadingType, ToTagValue(meterType)),
            new KeyValuePair<string, object?>(ProcessorTelemetry.Tags.ReadingLocation, ToTagValue(location)),
            new KeyValuePair<string, object?>(ProcessorTelemetry.Tags.Outcome, ToTagValue(outcome)));
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

    private static string ToTagValue(ReadingOutcome outcome)
    {
        return outcome switch
        {
            ReadingOutcome.Stored => "stored",
            ReadingOutcome.Duplicate => "duplicate",
            ReadingOutcome.Invalid => "invalid",
            _ => throw new UnreachableException($"No tag value for reading outcome {outcome}")
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

    private static string ToTagValue(MeterLocation location)
    {
        return location switch
        {
            MeterLocation.Kitchen => "kitchen",
            MeterLocation.LivingRoom => "living_room",
            MeterLocation.Bedroom => "bedroom",
            MeterLocation.Garage => "garage",
            MeterLocation.Office => "office",
            MeterLocation.Corridor => "corridor",
            _ => throw new UnreachableException($"No tag value for meter location {location}")
        };
    }
}
