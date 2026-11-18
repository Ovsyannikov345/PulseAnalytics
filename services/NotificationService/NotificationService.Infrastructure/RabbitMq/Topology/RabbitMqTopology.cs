namespace NotificationService.Infrastructure.RabbitMq.Topology;

internal static class RabbitMqTopology
{
    public static class Exchanges
    {
        public static class StoredReadingsExchange
        {
            public const string Name = "pulse.meter-readings.stored";

            public static class RoutingKeys
            {
                public const string MotionReadingStored = "meter-reading.motion.stored";

                public const string EnergyReadingStored = "meter-reading.energy.stored";

                public const string AirQualityReadingStored = "meter-reading.air-quality.stored";
            }
        }
    }

    /// <summary>
    /// Queues are per service instance (the instance ID is appended), so every instance gets every event for its own clients.
    /// </summary>
    public static class QueueNamePrefixes
    {
        public const string MotionReadingsNotification = "pulse.meter-readings.stored.motion.notify";

        public const string EnergyReadingsNotification = "pulse.meter-readings.stored.energy.notify";

        public const string AirQualityReadingsNotification = "pulse.meter-readings.stored.air-quality.notify";
    }

    public static class QueueArguments
    {
        public const string MessageTtl = "x-message-ttl";

        public const int MessageTtlInMilliseconds = 30_000;
    }

    public static string QueueName(string prefix, ServiceInstance instance)
    {
        return $"{prefix}.{instance.Id}";
    }
}
