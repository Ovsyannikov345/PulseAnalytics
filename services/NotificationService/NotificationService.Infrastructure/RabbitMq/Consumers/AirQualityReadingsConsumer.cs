using Ausiannikau.Utilities.Results;
using DataProcessor.Contracts.Events;
using Microsoft.Extensions.Logging;
using NotificationService.Application.LiveReadings;
using NotificationService.Application.LiveReadings.Models;
using NotificationService.Application.Telemetry.Metrics;
using NotificationService.Application.Telemetry.Traces;
using NotificationService.Infrastructure.RabbitMq.Connection;
using NotificationService.Infrastructure.RabbitMq.Mappers;
using NotificationService.Infrastructure.RabbitMq.Topology;

namespace NotificationService.Infrastructure.RabbitMq.Consumers;

internal sealed class AirQualityReadingsConsumer(
    IRabbitMqConnectionProvider connectionProvider,
    IReadingsBroadcastService broadcastService,
    ServiceInstance instance,
    TimeProvider timeProvider,
    IMetricsService metrics,
    ITracingService tracing,
    ILogger<AirQualityReadingsConsumer> logger)
    : StoredReadingsConsumer<AirQualityReadingStored>(connectionProvider, broadcastService, instance, timeProvider, metrics, tracing, logger)
{
    protected override string QueueNamePrefix => RabbitMqTopology.QueueNamePrefixes.AirQualityReadingsNotification;

    protected override string RoutingKey => RabbitMqTopology.Exchanges.StoredReadingsExchange.RoutingKeys.AirQualityReadingStored;

    protected override Result<LiveReadingModel> ToModel(AirQualityReadingStored message)
    {
        return LiveReadingMessageMapper.ToModel(message);
    }
}
