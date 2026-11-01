using Ausiannikau.Utilities.Results;
using DataIngestor.Contracts.Events;
using DataProcessor.Application.MeterReadings.Models;
using DataProcessor.Infrastructure.RabbitMq.Connection;
using DataProcessor.Infrastructure.RabbitMq.Mappers;
using DataProcessor.Infrastructure.RabbitMq.Topology;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DataProcessor.Infrastructure.RabbitMq.Consumers;

internal sealed class AirQualityReadingsConsumer(
    IRabbitMqConnectionProvider connectionProvider,
    IServiceScopeFactory scopeFactory,
    ILogger<AirQualityReadingsConsumer> logger)
    : MeterReadingsConsumer<AirQualityReadingReceived>(connectionProvider, scopeFactory, logger)
{
    protected override string QueueName => RabbitMqTopology.QueueNames.AirQualityReadingsProcessing;

    protected override string RoutingKey => RabbitMqTopology.Exchanges.MeterReadingsExchange.RoutingKeys.AirQualityReadingReceived;

    protected override Result<MeterReadingModel> ToModel(AirQualityReadingReceived message)
    {
        return MeterReadingMessageMapper.ToModel(message);
    }
}
