using Ausiannikau.Utilities.Results;
using NotificationService.Application.Telemetry.Metrics;
using NotificationService.Application.Telemetry.Traces;
using NotificationService.Infrastructure.RabbitMq.Connection;
using NotificationService.Infrastructure.RabbitMq.Consumers;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;

namespace NotificationService.UnitTests.TestDoubles;

/// <summary>
/// <see cref="BaseRabbitMqConsumer"/> that delegates queue setup and message processing to the test,
/// so the shared consuming logic can be exercised without a concrete consumer.
/// </summary>
internal sealed class DelegatingRabbitMqConsumer(
    IRabbitMqConnectionProvider connectionProvider,
    Func<IChannel, CancellationToken, Task<Result>> setupQueue,
    Func<ReceivedMessage, CancellationToken, Task<Result>> processMessage,
    TimeProvider timeProvider,
    IMetricsService metrics,
    ITracingService tracing,
    ILogger<DelegatingRabbitMqConsumer> logger)
    : BaseRabbitMqConsumer(connectionProvider, timeProvider, metrics, tracing, logger)
{
    public const string TestQueueName = "pulse.test-readings.notify";

    public const string TestMessageType = "TestReadingStored";

    protected override string QueueName => TestQueueName;

    protected override string MessageType => TestMessageType;

    protected override Task<Result> SetupQueueAsync(IChannel channel, CancellationToken ct)
    {
        return setupQueue(channel, ct);
    }

    protected override Task<Result> ProcessMessageAsync(ReceivedMessage message, CancellationToken ct)
    {
        return processMessage(message, ct);
    }
}
