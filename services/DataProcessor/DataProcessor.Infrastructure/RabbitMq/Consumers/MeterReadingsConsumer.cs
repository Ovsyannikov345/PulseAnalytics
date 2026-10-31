using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Text;
using System.Threading.Channels;
using Ausiannikau.Utilities.Results;
using DataProcessor.Infrastructure.RabbitMq.Connection;
using DataProcessor.Infrastructure.RabbitMq.Topology;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace DataProcessor.Infrastructure.RabbitMq.Consumers;

internal class MeterReadingsConsumer(IRabbitMqConnectionProvider connectionProvider, ILogger<MeterReadingsConsumer> logger)
    : BaseRabbitMqConsumer(connectionProvider, logger)
{
    protected override string QueueName => RabbitMqTopology.QueueNames.MeterReadingsProcessing;

    protected override async Task<Result> SetupQueueAsync(IChannel channel, CancellationToken ct)
    {
        await channel.QueueDeclareAsync(
            queue: QueueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            cancellationToken: ct);

        await channel.QueueBindAsync(
            queue: QueueName,
            exchange: RabbitMqTopology.Exchanges.MeterReadingsExchange.Name,
            routingKey: RabbitMqTopology.Exchanges.MeterReadingsExchange.RoutingKeys.AnyReadingReceived,
            cancellationToken: ct);

        await channel.BasicQosAsync(
            prefetchSize: 0,
            prefetchCount: 10,
            global: false,
            cancellationToken: ct);

        return Result.Succeed();
    }

    protected override Task<Result> ProcessMessageAsync(BasicDeliverEventArgs message)
    {
        var info = message.BasicProperties;

        // TODO replace with real handling
        Logger.LogInformation(
            "Processed a new reading. App ID: {AppId}. Content type: {ContentType}. Delivery mode: {Mode}. " +
            "Message ID: {Id}. Correlation ID: {CorrelationId}. Type: {Type}. Timestamp: {Timestamp}. Body: {Body}",
            info.AppId,
            info.ContentType,
            info.DeliveryMode,
            info.MessageId,
            info.CorrelationId,
            info.Type,
            info.Timestamp,
            message.Body.ToString());

        return Task.FromResult(Result.Succeed());
    }
}
