using System.Diagnostics;

namespace DataIngestor.Application.Telemetry.Traces;

public interface ITracingService
{
    public Activity? StartPoll(Guid correlationId);
}

internal sealed class TracingService : ITracingService, IDisposable
{
    private readonly ActivitySource _activitySource = new(IngestorTelemetry.ScopeName);

    public Activity? StartPoll(Guid correlationId)
    {
        return _activitySource.StartActivity(
            IngestorTelemetry.SpanNames.Polling,
            ActivityKind.Internal,
            parentContext: default,
            tags: [new(IngestorTelemetry.Tags.CorrelationId, correlationId.ToString())]);
    }

    public void Dispose()
    {
        _activitySource.Dispose();
    }
}
