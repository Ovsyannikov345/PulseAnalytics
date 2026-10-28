using System.Collections.Concurrent;
using DataIngestor.IntegrationTests.Fixtures;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace DataIngestor.IntegrationTests.Utilities;

/// <summary>
/// A temporary queue bound to the meter readings exchange that collects every message routed to it,
/// the way a downstream consumer would receive them. The queue is deleted when it is disposed.
/// </summary>
internal sealed class MeterReadingsQueue : IAsyncDisposable
{
    public const string ExchangeName = "pulse.meter-readings";

    public const string AllReadingsBindingKey = "meter-reading.*.received";

    private static readonly TimeSpan DeliveryTimeout = TimeSpan.FromSeconds(30);

    private readonly ConcurrentQueue<ReceivedMessage> _messages = new();
    private readonly IConnection _connection;
    private readonly IChannel _channel;

    private MeterReadingsQueue(IConnection connection, IChannel channel)
    {
        _connection = connection;
        _channel = channel;
    }

    public IReadOnlyList<ReceivedMessage> Messages => [.. _messages];

    public static async Task<MeterReadingsQueue> CreateAsync(
        RabbitMqContainerFixture rabbitMq,
        CancellationToken ct,
        string bindingKey = AllReadingsBindingKey)
    {
        var connection = await rabbitMq.CreateConnectionAsync(ct);
        var channel = await connection.CreateChannelAsync(cancellationToken: ct);

        var queue = new MeterReadingsQueue(connection, channel);

        // Declared with the same arguments as the ingestor, so binding works before its first publish.
        await channel.ExchangeDeclareAsync(ExchangeName, ExchangeType.Topic, durable: true, autoDelete: false, cancellationToken: ct);

        var declaration = await channel.QueueDeclareAsync(
            queue: string.Empty,
            durable: false,
            exclusive: true,
            autoDelete: true,
            cancellationToken: ct);

        await channel.QueueBindAsync(declaration.QueueName, ExchangeName, bindingKey, cancellationToken: ct);

        var consumer = new AsyncEventingBasicConsumer(channel);
        consumer.ReceivedAsync += queue.OnReceivedAsync;

        await channel.BasicConsumeAsync(declaration.QueueName, autoAck: true, consumer, ct);

        return queue;
    }

    public async Task<IReadOnlyList<ReceivedMessage>> WaitForMessagesAsync(int count, CancellationToken ct)
    {
        await TestWait.UntilAsync(() => _messages.Count >= count, DeliveryTimeout, ct);

        return Messages;
    }

    public async ValueTask DisposeAsync()
    {
        await _channel.CloseAsync();
        await _channel.DisposeAsync();

        await _connection.CloseAsync();
        await _connection.DisposeAsync();
    }

    private Task OnReceivedAsync(object sender, BasicDeliverEventArgs args)
    {
        // The body buffer is reused by the client once the handler returns, so it is copied.
        _messages.Enqueue(new ReceivedMessage(args.RoutingKey, new BasicProperties(args.BasicProperties), args.Body.ToArray()));

        return Task.CompletedTask;
    }
}

internal sealed record ReceivedMessage(string RoutingKey, IReadOnlyBasicProperties Properties, byte[] Body);
