namespace DataProcessor.Infrastructure.RabbitMq.Connection;

/// <summary>
/// Service keys of the two RabbitMQ connections. Publishing has a connection of its own, because the broker blocks
/// publishing connections during a memory or disk alarm, and consumers sharing it would stop acking too.
/// </summary>
internal static class RabbitMqConnectionKeys
{
    public const string Consuming = "consuming";

    public const string Publishing = "publishing";
}
