using System.Diagnostics;

namespace DataProcessor.Application.Telemetry.Traces;

public interface ITracingService
{
    public Activity? StartMessageProcessing(string messageType, string? messageId, Guid correlationId);
}

internal sealed class TracingService : ITracingService, IDisposable
{
    private readonly ActivitySource _activitySource = new(ProcessorTelemetry.ScopeName);

    public Activity? StartMessageProcessing(string messageType, string? messageId, Guid correlationId)
    {
        return _activitySource.StartActivity(
            ProcessorTelemetry.SpanNames.Processing,
            ActivityKind.Internal,
            parentContext: default,
            tags:
            [
                new(ProcessorTelemetry.Tags.MessageType, messageType),
                new(ProcessorTelemetry.Tags.MessageId, messageId),
                new(ProcessorTelemetry.Tags.CorrelationId, correlationId.ToString())
            ]);
    }

    public void Dispose()
    {
        _activitySource.Dispose();
    }
}
