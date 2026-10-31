using Ausiannikau.Utilities.Results;
using DataProcessor.Infrastructure.RabbitMq.Connection;
using DataProcessor.Infrastructure.RabbitMq.Consumers;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;

namespace DataProcessor.UnitTests.TestDoubles;

/// <summary>
/// <see cref="BaseRabbitMqConsumer"/> that delegates queue setup and message processing to the test,
/// so the shared consuming logic can be exercised without a concrete consumer.
/// </summary>
internal sealed class DelegatingRabbitMqConsumer(
    IRabbitMqConnectionProvider connectionProvider,
    Func<IChannel, CancellationToken, Task<Result>> setupQueue,
    Func<ReceivedMessage, CancellationToken, Task<Result>> processMessage,
    ILogger<DelegatingRabbitMqConsumer> logger)
    : BaseRabbitMqConsumer(connectionProvider, logger)
{
    public const string TestQueueName = "pulse.test-readings.process";

    protected override string QueueName => TestQueueName;

    protected override Task<Result> SetupQueueAsync(IChannel channel, CancellationToken ct)
    {
        return setupQueue(channel, ct);
    }

    protected override Task<Result> ProcessMessageAsync(ReceivedMessage message, CancellationToken ct)
    {
        return processMessage(message, ct);
    }
}
