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

internal sealed class MotionReadingsConsumer(
    IRabbitMqConnectionProvider connectionProvider,
    IReadingsBroadcastService broadcastService,
    ServiceInstance instance,
    TimeProvider timeProvider,
    IMetricsService metrics,
    ITracingService tracing,
    ILogger<MotionReadingsConsumer> logger)
    : StoredReadingsConsumer<MotionReadingStored>(connectionProvider, broadcastService, instance, timeProvider, metrics, tracing, logger)
{
    protected override string QueueNamePrefix => RabbitMqTopology.QueueNamePrefixes.MotionReadingsNotification;

    protected override string RoutingKey => RabbitMqTopology.Exchanges.StoredReadingsExchange.RoutingKeys.MotionReadingStored;

    protected override Result<LiveReadingModel> ToModel(MotionReadingStored message)
    {
        return LiveReadingMessageMapper.ToModel(message);
    }
}
