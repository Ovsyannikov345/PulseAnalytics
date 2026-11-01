using DataProcessor.Infrastructure.RabbitMq.Connection;
using DataProcessor.Infrastructure.RabbitMq.Topology;
using Microsoft.Extensions.Logging;

namespace DataProcessor.Infrastructure.RabbitMq.Consumers;

internal sealed class AirQualityReadingsConsumer(
    IRabbitMqConnectionProvider connectionProvider,
    ILogger<AirQualityReadingsConsumer> logger)
    : MeterReadingsConsumer(connectionProvider, logger)
{
    protected override string QueueName => RabbitMqTopology.QueueNames.AirQualityReadingsProcessing;

    protected override string RoutingKey => RabbitMqTopology.Exchanges.MeterReadingsExchange.RoutingKeys.AirQualityReadingReceived;
}
