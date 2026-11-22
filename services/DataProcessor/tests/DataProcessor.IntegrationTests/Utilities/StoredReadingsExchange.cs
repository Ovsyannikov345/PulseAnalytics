using System.Text;
using System.Text.Json;
using DataProcessor.Contracts.Events;
using DataProcessor.IntegrationTests.Fixtures;
using RabbitMQ.Client;

namespace DataProcessor.IntegrationTests.Utilities;

/// <summary>
/// Listens to the processor's stored readings exchange through a test queue of its own,
/// which the broker deletes when this instance is disposed.
/// </summary>
internal sealed class StoredReadingsExchange : IAsyncDisposable
{
    public const string Name = "pulse.meter-readings.stored";

    private const string AllStoredReadingsRoutingKey = "meter-reading.*.stored";

    private static readonly TimeSpan DeliveryTimeout = TimeSpan.FromSeconds(30);

    private readonly IConnection _connection;
    private readonly IChannel _channel;
    private readonly string _queue;
    private readonly List<StoredEventMessage> _received = [];

    private StoredReadingsExchange(IConnection connection, IChannel channel, string queue)
    {
        _connection = connection;
        _channel = channel;
        _queue = queue;
    }

    public static async Task<StoredReadingsExchange> CreateAsync(RabbitMqContainerFixture rabbitMq, CancellationToken ct)
    {
        var connection = await rabbitMq.CreateConnectionAsync(ct);
        var channel = await connection.CreateChannelAsync(cancellationToken: ct);

        // Declared with the same arguments as the processor, so the binding can be made before its first publish.
        await channel.ExchangeDeclareAsync(Name, ExchangeType.Topic, durable: true, autoDelete: false, cancellationToken: ct);

        var queue = await channel.QueueDeclareAsync(
            queue: string.Empty,
            durable: false,
            exclusive: true,
            autoDelete: true,
            cancellationToken: ct);

        await channel.QueueBindAsync(queue.QueueName, Name, AllStoredReadingsRoutingKey, cancellationToken: ct);

        return new StoredReadingsExchange(connection, channel, queue.QueueName);
    }

    public static async Task DeleteAsync(RabbitMqContainerFixture rabbitMq, CancellationToken ct)
    {
        await using var connection = await rabbitMq.CreateConnectionAsync(ct);
        await using var channel = await connection.CreateChannelAsync(cancellationToken: ct);

        await channel.ExchangeDeleteAsync(Name, cancellationToken: ct);
    }

    /// <summary>
    /// Waits until a stored event for each of the given readings has arrived, and returns every event received for them
    /// (events of other tests' readings, e.g. redelivered leftovers, are left out).
    /// </summary>
    public async Task<IReadOnlyList<StoredEventMessage>> WaitForReadingsAsync(
        IReadOnlyCollection<Guid> readingIds,
        CancellationToken ct)
    {
        await TestWait.UntilAsync(
            async () =>
            {
                await ReceiveReadyMessagesAsync(ct);

                return readingIds.All(readingId => _received.Exists(message => message.Event.ReadingId == readingId));
            },
            DeliveryTimeout,
            ct);

        return [.. _received.Where(message => readingIds.Contains(message.Event.ReadingId))];
    }

    public async ValueTask DisposeAsync()
    {
        await _channel.CloseAsync();
        await _channel.DisposeAsync();

        await _connection.CloseAsync();
        await _connection.DisposeAsync();
    }

    private async Task ReceiveReadyMessagesAsync(CancellationToken ct)
    {
        while (await _channel.BasicGetAsync(_queue, autoAck: true, ct) is { } message)
        {
            _received.Add(new StoredEventMessage(message.RoutingKey, message.BasicProperties, message.Body.ToArray()));
        }
    }
}

internal sealed record StoredEventMessage(string RoutingKey, IReadOnlyBasicProperties Properties, byte[] Body)
{
    public MeterReadingStoredEvent Event => Properties.Type switch
    {
        nameof(MotionReadingStored) => Deserialize<MotionReadingStored>(),
        nameof(EnergyReadingStored) => Deserialize<EnergyReadingStored>(),
        nameof(AirQualityReadingStored) => Deserialize<AirQualityReadingStored>(),
        _ => throw new InvalidOperationException($"Unexpected message type {Properties.Type}"),
    };

    /// <summary>
    /// Gets the W3C trace context header that RabbitMQ.Client writes when it publishes within a trace.
    /// </summary>
    public string? TraceParent => Properties.Headers?.TryGetValue("traceparent", out var value) == true && value is byte[] bytes
        ? Encoding.UTF8.GetString(bytes)
        : null;

    private TEvent Deserialize<TEvent>()
        where TEvent : MeterReadingStoredEvent
    {
        return JsonSerializer.Deserialize<TEvent>(Body, JsonSerializerOptions.Web)
            ?? throw new InvalidOperationException($"Body is not a {typeof(TEvent).Name}");
    }
}
