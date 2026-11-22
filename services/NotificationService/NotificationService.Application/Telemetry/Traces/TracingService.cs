using System.Diagnostics;

namespace NotificationService.Application.Telemetry.Traces;

public interface ITracingService
{
    public Activity? StartMessageProcessing(string messageType, string? messageId, Guid correlationId);
}

internal sealed class TracingService : ITracingService, IDisposable
{
    private readonly ActivitySource _activitySource = new(NotificationServiceTelemetry.ScopeName);

    public Activity? StartMessageProcessing(string messageType, string? messageId, Guid correlationId)
    {
        return _activitySource.StartActivity(
            NotificationServiceTelemetry.SpanNames.Broadcast,
            ActivityKind.Internal,
            parentContext: default,
            tags:
            [
                new(NotificationServiceTelemetry.Tags.MessageType, messageType),
                new(NotificationServiceTelemetry.Tags.MessageId, messageId),
                new(NotificationServiceTelemetry.Tags.CorrelationId, correlationId.ToString()),
            ]);
    }

    public void Dispose()
    {
        _activitySource.Dispose();
    }
}
