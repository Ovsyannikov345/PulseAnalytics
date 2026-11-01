using Ausiannikau.Utilities.Results;
using DataIngestor.Contracts.Events;
using DataProcessor.Application.Readings.Models;
using DataProcessor.Infrastructure.RabbitMq.Connection;
using DataProcessor.Infrastructure.RabbitMq.Mappers;
using DataProcessor.Infrastructure.RabbitMq.Topology;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DataProcessor.Infrastructure.RabbitMq.Consumers;

internal sealed class MotionReadingsConsumer(
    IRabbitMqConnectionProvider connectionProvider,
    IServiceScopeFactory scopeFactory,
    ILogger<MotionReadingsConsumer> logger)
    : MeterReadingsConsumer<MotionReadingReceived>(connectionProvider, scopeFactory, logger)
{
    protected override string QueueName => RabbitMqTopology.QueueNames.MotionReadingsProcessing;

    protected override string RoutingKey => RabbitMqTopology.Exchanges.MeterReadingsExchange.RoutingKeys.MotionReadingReceived;

    protected override Result<MeterReadingModel> ToModel(MotionReadingReceived message)
    {
        return MeterReadingMessageMapper.ToModel(message);
    }
}
