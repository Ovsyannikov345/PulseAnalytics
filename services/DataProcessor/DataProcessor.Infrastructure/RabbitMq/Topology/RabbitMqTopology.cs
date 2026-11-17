namespace DataProcessor.Infrastructure.RabbitMq.Topology;

internal static class RabbitMqTopology
{
    public static class Exchanges
    {
        public static class MeterReadingsExchange
        {
            public const string Name = "pulse.meter-readings";

            public static class RoutingKeys
            {
                public const string MotionReadingReceived = "meter-reading.motion.received";

                public const string EnergyReadingReceived = "meter-reading.energy.received";

                public const string AirQualityReadingReceived = "meter-reading.air-quality.received";
            }
        }

        // Owned by the processor: declared on the first publish.
        public static class StoredReadingsExchange
        {
            public const string Name = "pulse.meter-readings.stored";

            public const string Type = RabbitMQ.Client.ExchangeType.Topic;

            // Each reading type has its own routing key, so consumers can bind a separate queue per type
            // or use the "meter-reading.*.stored" pattern to receive all of them.
            public static class RoutingKeys
            {
                public const string MotionReadingStored = "meter-reading.motion.stored";

                public const string EnergyReadingStored = "meter-reading.energy.stored";

                public const string AirQualityReadingStored = "meter-reading.air-quality.stored";
            }
        }
    }

    public static class QueueNames
    {
        public const string MotionReadingsProcessing = "pulse.meter-readings.motion.process";

        public const string EnergyReadingsProcessing = "pulse.meter-readings.energy.process";

        public const string AirQualityReadingsProcessing = "pulse.meter-readings.air-quality.process";
    }
}
