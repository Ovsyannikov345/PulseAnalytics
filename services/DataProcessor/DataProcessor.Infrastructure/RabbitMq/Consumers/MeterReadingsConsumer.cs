using System.Text;
using Ausiannikau.Utilities.Results;
using DataProcessor.Infrastructure.RabbitMq.Connection;
using DataProcessor.Infrastructure.RabbitMq.Topology;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;

namespace DataProcessor.Infrastructure.RabbitMq.Consumers;

internal abstract class MeterReadingsConsumer(IRabbitMqConnectionProvider connectionProvider, ILogger logger)
    : BaseRabbitMqConsumer(connectionProvider, logger)
{
    protected abstract string RoutingKey { get; }

    protected override async Task<Result> SetupQueueAsync(IChannel channel, CancellationToken ct)
    {
        await channel.ExchangeDeclarePassiveAsync(RabbitMqTopology.Exchanges.MeterReadingsExchange.Name, ct);

        await channel.QueueDeclareAsync(
            queue: QueueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            cancellationToken: ct);

        await channel.QueueBindAsync(
            queue: QueueName,
            exchange: RabbitMqTopology.Exchanges.MeterReadingsExchange.Name,
            routingKey: RoutingKey,
            cancellationToken: ct);

        await channel.BasicQosAsync(
            prefetchSize: 0,
            prefetchCount: 10,
            global: false,
            cancellationToken: ct);

        return Result.Succeed();
    }
}
