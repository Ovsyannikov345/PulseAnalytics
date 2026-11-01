using DataProcessor.Infrastructure.RabbitMq.Connection;
using DataProcessor.Infrastructure.RabbitMq.Topology;
using Microsoft.Extensions.Logging;

namespace DataProcessor.Infrastructure.RabbitMq.Consumers;

internal sealed class EnergyReadingsConsumer(
    IRabbitMqConnectionProvider connectionProvider,
    ILogger<EnergyReadingsConsumer> logger)
    : MeterReadingsConsumer(connectionProvider, logger)
{
    protected override string QueueName => RabbitMqTopology.QueueNames.EnergyReadingsProcessing;

    protected override string RoutingKey => RabbitMqTopology.Exchanges.MeterReadingsExchange.RoutingKeys.EnergyReadingReceived;
}
