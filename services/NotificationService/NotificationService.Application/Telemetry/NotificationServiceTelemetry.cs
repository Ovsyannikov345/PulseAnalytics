namespace NotificationService.Application.Telemetry;

public static class NotificationServiceTelemetry
{
    public const string ServiceName = "notification-service";

    public const string ScopeName = "PulseAnalytics.NotificationService";

    public static class Tags
    {
        public const string Outcome = "app.outcome";

        public const string ReadingType = "app.reading.type";

        public const string MessageType = "app.message.type";

        public const string CorrelationId = "app.correlation_id";

        public const string MessageId = "messaging.message.id";

        public const string ErrorType = "error.type";
    }

    public static class SpanNames
    {
        public const string Broadcast = "broadcast meter reading";
    }
}
