using System.Text.Json;
using DataIngestor.Contracts.Events;
using DataProcessor.IntegrationTests.Fixtures;
using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;

namespace DataProcessor.IntegrationTests.Utilities;

/// <summary>
/// Publishes to the meter readings exchange the way the ingestor does, and inspects the processor's queues.
/// </summary>
internal sealed class MeterReadingsExchange : IAsyncDisposable
{
    public const string Name = "pulse.meter-readings";

    public const string MotionRoutingKey = "meter-reading.motion.received";

    public const string EnergyRoutingKey = "meter-reading.energy.received";

    public const string AirQualityRoutingKey = "meter-reading.air-quality.received";

    public const string MotionQueue = "pulse.meter-readings.motion.process";

    public const string EnergyQueue = "pulse.meter-readings.energy.process";

    public const string AirQualityQueue = "pulse.meter-readings.air-quality.process";

    public static readonly IReadOnlyList<string> ProcessingQueues = [MotionQueue, EnergyQueue, AirQualityQueue];

    private static readonly Dictionary<Type, string> RoutingKeys = new()
    {
        [typeof(MotionReadingReceived)] = MotionRoutingKey,
        [typeof(EnergyReadingReceived)] = EnergyRoutingKey,
        [typeof(AirQualityReadingReceived)] = AirQualityRoutingKey
    };

    private static readonly TimeSpan ConsumersStartTimeout = TimeSpan.FromSeconds(30);

    private readonly IConnection _connection;
    private readonly IChannel _channel;

    private MeterReadingsExchange(IConnection connection, IChannel channel)
    {
        _connection = connection;
        _channel = channel;
    }

    public static async Task<MeterReadingsExchange> CreateAsync(RabbitMqContainerFixture rabbitMq, CancellationToken ct)
    {
        var connection = await rabbitMq.CreateConnectionAsync(ct);
        var channel = await connection.CreateChannelAsync(cancellationToken: ct);

        // Declared with the same arguments as the ingestor; the processor only declares it passively.
        await channel.ExchangeDeclareAsync(Name, ExchangeType.Topic, durable: true, autoDelete: false, cancellationToken: ct);

        return new MeterReadingsExchange(connection, channel);
    }

    public static async Task DeleteAsync(RabbitMqContainerFixture rabbitMq, CancellationToken ct)
    {
        await using var connection = await rabbitMq.CreateConnectionAsync(ct);
        await using var channel = await connection.CreateChannelAsync(cancellationToken: ct);

        await channel.ExchangeDeleteAsync(Name, cancellationToken: ct);
    }

    public async Task PublishAsync<TEvent>(TEvent readingEvent, CancellationToken ct)
        where TEvent : MeterReadingEvent
    {
        var body = JsonSerializer.SerializeToUtf8Bytes(readingEvent, JsonSerializerOptions.Web);

        await PublishAsync(RoutingKeys[typeof(TEvent)], readingEvent.MessageId, typeof(TEvent).Name, body, ct);
    }

    public async Task PublishRawAsync(string routingKey, byte[] body, CancellationToken ct)
    {
        await PublishAsync(routingKey, Guid.CreateVersion7(), "Unknown", body, ct);
    }

    /// <summary>
    /// Waits until the processor has a consumer on each of its queues; a queue gets its binding before its consumer,
    /// and messages published earlier are not routed to it.
    /// </summary>
    public async Task WaitForConsumersAsync(CancellationToken ct)
    {
        await TestWait.UntilAsync(
            async () =>
            {
                foreach (var queue in ProcessingQueues)
                {
                    if (await GetQueueStateAsync(queue, ct) is not { ConsumerCount: 1 })
                    {
                        return false;
                    }
                }

                return true;
            },
            ConsumersStartTimeout,
            ct);
    }

    public async Task<uint> GetReadyMessageCountAsync(string queue, CancellationToken ct)
    {
        var state = await GetQueueStateAsync(queue, ct);

        return state?.MessageCount ?? throw new InvalidOperationException($"Queue {queue} does not exist");
    }

    public async Task PurgeAsync(string queue, CancellationToken ct)
    {
        await _channel.QueuePurgeAsync(queue, ct);
    }

    public async ValueTask DisposeAsync()
    {
        await _channel.CloseAsync();
        await _channel.DisposeAsync();

        await _connection.CloseAsync();
        await _connection.DisposeAsync();
    }

    private async Task PublishAsync(string routingKey, Guid messageId, string messageType, byte[] body, CancellationToken ct)
    {
        var properties = new BasicProperties
        {
            MessageId = messageId.ToString(),
            CorrelationId = Guid.CreateVersion7().ToString(),
            ContentType = "application/json",
            DeliveryMode = DeliveryModes.Persistent,
            Type = messageType
        };

        await _channel.BasicPublishAsync(Name, routingKey, mandatory: false, properties, body, ct);
    }

    // A passive declare of a missing queue closes the channel, so every check gets a channel of its own.
    private async Task<QueueDeclareOk?> GetQueueStateAsync(string queue, CancellationToken ct)
    {
        await using var channel = await _connection.CreateChannelAsync(cancellationToken: ct);

        try
        {
            return await channel.QueueDeclarePassiveAsync(queue, ct);
        }
        catch (OperationInterruptedException)
        {
            return null;
        }
    }
}
