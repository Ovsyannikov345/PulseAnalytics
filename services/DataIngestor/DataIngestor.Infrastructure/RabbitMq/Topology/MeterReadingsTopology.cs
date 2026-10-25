namespace DataIngestor.Infrastructure.RabbitMq.Topology;

internal static class MeterReadingsTopology
{
    public const string ExchangeName = "pulse.meter-readings";

    public const string ExchangeType = RabbitMQ.Client.ExchangeType.Topic;

    // Each reading type has its own routing key, so consumers can bind a separate queue per type
    // or use the "meter-reading.*.received" pattern to receive all of them.
    internal static class RoutingKeys
    {
        public const string MotionReadingReceived = "meter-reading.motion.received";

        public const string EnergyReadingReceived = "meter-reading.energy.received";

        public const string AirQualityReadingReceived = "meter-reading.air-quality.received";
    }
}
