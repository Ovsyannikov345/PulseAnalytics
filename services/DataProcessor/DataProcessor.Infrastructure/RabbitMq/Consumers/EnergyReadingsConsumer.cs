using Ausiannikau.Utilities.Results;
using DataIngestor.Contracts.Events;
using DataProcessor.Application.MeterReadings.Models;
using DataProcessor.Infrastructure.RabbitMq.Connection;
using DataProcessor.Infrastructure.RabbitMq.Mappers;
using DataProcessor.Infrastructure.RabbitMq.Topology;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DataProcessor.Infrastructure.RabbitMq.Consumers;

internal sealed class EnergyReadingsConsumer(
    IRabbitMqConnectionProvider connectionProvider,
    IServiceScopeFactory scopeFactory,
    ILogger<EnergyReadingsConsumer> logger)
    : MeterReadingsConsumer<EnergyReadingReceived>(connectionProvider, scopeFactory, logger)
{
    protected override string QueueName => RabbitMqTopology.QueueNames.EnergyReadingsProcessing;

    protected override string RoutingKey => RabbitMqTopology.Exchanges.MeterReadingsExchange.RoutingKeys.EnergyReadingReceived;

    protected override Result<MeterReadingModel> ToModel(EnergyReadingReceived message)
    {
        return MeterReadingMessageMapper.ToModel(message);
    }
}
