namespace DataIngestor.Application.Telemetry;

public static class IngestorTelemetry
{
    public const string ServiceName = "data-ingestor";

    public const string ScopeName = "PulseAnalytics.DataIngestor";

    public static class Tags
    {
        public const string Outcome = "app.outcome";

        public const string ReadingType = "app.reading.type";

        public const string MessageType = "app.message.type";

        public const string CorrelationId = "app.correlation_id";

        public const string ErrorType = "error.type";
    }

    public static class SpanNames
    {
        public const string Polling = "poll meters";
    }
}
