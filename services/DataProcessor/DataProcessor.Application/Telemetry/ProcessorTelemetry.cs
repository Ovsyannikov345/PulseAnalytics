namespace DataProcessor.Application.Telemetry;

public static class ProcessorTelemetry
{
    public const string ServiceName = "data-processor";

    public const string ScopeName = "PulseAnalytics.DataProcessor";

    public static class Tags
    {
        public const string Outcome = "app.outcome";

        public const string ReadingType = "app.reading.type";

        public const string ReadingLocation = "app.reading.location";

        public const string MessageType = "app.message.type";

        public const string CorrelationId = "app.correlation_id";

        public const string MessageId = "messaging.message.id";

        public const string ErrorType = "error.type";
    }

    public static class SpanNames
    {
        public const string Processing = "process meter reading";
    }
}
