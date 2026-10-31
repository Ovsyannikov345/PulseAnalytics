using System;
using System.Collections.Generic;
using System.Text;

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
                public const string AnyReadingReceived = "meter-reading.*.received";
            }
        }
    }

    public static class QueueNames
    {
        public const string MeterReadingsProcessing = "pulse.meter-readings.process";
    }
}
