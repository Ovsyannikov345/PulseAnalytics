using System.Text;
using Ausiannikau.Utilities.Results;
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

    protected override Task<Result> ProcessMessageAsync(ReceivedMessage message, CancellationToken ct)
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
            Encoding.UTF8.GetString(message.Body.Span));

        return Task.FromResult(Result.Succeed());
    }
}
