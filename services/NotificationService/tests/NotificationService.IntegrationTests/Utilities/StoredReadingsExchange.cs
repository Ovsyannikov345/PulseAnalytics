using System.Text.Json;
using DataProcessor.Contracts.Events;
using NotificationService.IntegrationTests.Fixtures;
using RabbitMQ.Client;

namespace NotificationService.IntegrationTests.Utilities;

/// <summary>
/// Publishes to the stored readings exchange the way the data processor does.
/// </summary>
internal sealed class StoredReadingsExchange : IAsyncDisposable
{
    public const string Name = "pulse.meter-readings.stored";

    public const string MotionRoutingKey = "meter-reading.motion.stored";

    public const string EnergyRoutingKey = "meter-reading.energy.stored";

    public const string AirQualityRoutingKey = "meter-reading.air-quality.stored";

    private const string ApplicationId = "data-processor";

    private static readonly Dictionary<Type, string> RoutingKeys = new()
    {
        [typeof(MotionReadingStored)] = MotionRoutingKey,
        [typeof(EnergyReadingStored)] = EnergyRoutingKey,
        [typeof(AirQualityReadingStored)] = AirQualityRoutingKey
    };

    private readonly IConnection _connection;
    private readonly IChannel _channel;

    private StoredReadingsExchange(IConnection connection, IChannel channel)
    {
        _connection = connection;
        _channel = channel;
    }

    public static async Task<StoredReadingsExchange> CreateAsync(RabbitMqContainerFixture rabbitMq, CancellationToken ct)
    {
        var connection = await rabbitMq.CreateConnectionAsync(ct);
        var channel = await connection.CreateChannelAsync(cancellationToken: ct);

        await DeclareAsync(channel, ct);

        return new StoredReadingsExchange(connection, channel);
    }

    /// <summary>
    /// Declares the exchange with the same arguments as the processor; the notification service only declares it passively.
    /// </summary>
    public static async Task DeclareAsync(RabbitMqContainerFixture rabbitMq, CancellationToken ct)
    {
        await using var connection = await rabbitMq.CreateConnectionAsync(ct);
        await using var channel = await connection.CreateChannelAsync(cancellationToken: ct);

        await DeclareAsync(channel, ct);
    }

    public static async Task DeleteAsync(RabbitMqContainerFixture rabbitMq, CancellationToken ct)
    {
        await using var connection = await rabbitMq.CreateConnectionAsync(ct);
        await using var channel = await connection.CreateChannelAsync(cancellationToken: ct);

        await channel.ExchangeDeleteAsync(Name, cancellationToken: ct);
    }

    public async Task PublishAsync<TEvent>(TEvent storedEvent, CancellationToken ct)
        where TEvent : MeterReadingStoredEvent
    {
        var body = JsonSerializer.SerializeToUtf8Bytes(storedEvent, JsonSerializerOptions.Web);

        await PublishAsync(RoutingKeys[typeof(TEvent)], storedEvent.MessageId, typeof(TEvent).Name, body, ct);
    }

    /// <returns>The message ID the message was published with.</returns>
    public async Task<Guid> PublishRawAsync(string routingKey, byte[] body, CancellationToken ct)
    {
        var messageId = Guid.CreateVersion7();

        await PublishAsync(routingKey, messageId, "Unknown", body, ct);

        return messageId;
    }

    public async ValueTask DisposeAsync()
    {
        await _channel.CloseAsync();
        await _channel.DisposeAsync();

        await _connection.CloseAsync();
        await _connection.DisposeAsync();
    }

    private static async Task DeclareAsync(IChannel channel, CancellationToken ct)
    {
        await channel.ExchangeDeclareAsync(Name, ExchangeType.Topic, durable: true, autoDelete: false, cancellationToken: ct);
    }

    private async Task PublishAsync(string routingKey, Guid messageId, string messageType, byte[] body, CancellationToken ct)
    {
        var properties = new BasicProperties
        {
            MessageId = messageId.ToString(),
            Type = messageType,
            AppId = ApplicationId,
            CorrelationId = Guid.CreateVersion7().ToString(),
            ContentType = "application/json",
            DeliveryMode = DeliveryModes.Transient,
            Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds())
        };

        await _channel.BasicPublishAsync(Name, routingKey, mandatory: false, properties, body, ct);
    }
}
