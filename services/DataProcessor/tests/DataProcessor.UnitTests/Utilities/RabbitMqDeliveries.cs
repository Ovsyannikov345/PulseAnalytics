using RabbitMQ.Client;

namespace DataProcessor.UnitTests.Utilities;

/// <summary>
/// Hands messages to a registered consumer the same way the RabbitMQ client dispatches broker deliveries.
/// </summary>
internal static class RabbitMqDeliveries
{
    public const string ConsumerTag = "amq.ctag-test";

    public const string MessageId = "0199f5c4-3a1e-7b52-9d0e-5c8a7e6b4f21";

    public const string MessageType = "MotionReadingReceived";

    public static BasicProperties Properties(string? correlationId = null)
    {
        return new BasicProperties
        {
            MessageId = MessageId,
            Type = MessageType,
            ContentType = "application/json",
            CorrelationId = correlationId ?? Guid.CreateVersion7().ToString()
        };
    }

    public static Task DeliverAsync(
        this IAsyncBasicConsumer consumer,
        ulong deliveryTag,
        IReadOnlyBasicProperties properties,
        CancellationToken ct)
    {
        return consumer.DeliverAsync(deliveryTag, properties, "{}"u8.ToArray(), ct);
    }

    public static Task DeliverAsync(
        this IAsyncBasicConsumer consumer,
        ulong deliveryTag,
        IReadOnlyBasicProperties properties,
        ReadOnlyMemory<byte> body,
        CancellationToken ct)
    {
        return consumer.HandleBasicDeliverAsync(
            ConsumerTag,
            deliveryTag,
            redelivered: false,
            exchange: "pulse.meter-readings",
            routingKey: "meter-reading.motion.received",
            properties,
            body,
            ct);
    }
}
