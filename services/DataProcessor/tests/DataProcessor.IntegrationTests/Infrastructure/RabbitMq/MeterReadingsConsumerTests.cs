using DataProcessor.IntegrationTests.Fixtures;
using DataProcessor.IntegrationTests.Utilities;
using Shouldly;
using Xunit;

namespace DataProcessor.IntegrationTests.Infrastructure.RabbitMq;

public sealed class MeterReadingsConsumerTests(RabbitMqContainerFixture rabbitMq, PostgreSqlContainerFixture postgreSql)
{
    private static readonly TimeSpan RoutingTimeout = TimeSpan.FromSeconds(10);

    [Fact]
    public async Task StartAsync_ExchangeExists_ConsumesDurableQueues()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        await using var exchange = await MeterReadingsExchange.CreateAsync(rabbitMq, ct);
        await using var factory = new ProcessorApplicationFactory(rabbitMq, postgreSql);

        // Act
        factory.StartServer();
        await exchange.WaitForConsumersAsync(ct);

        // Assert
        await using var connection = await rabbitMq.CreateConnectionAsync(ct);
        await using var channel = await connection.CreateChannelAsync(cancellationToken: ct);

        // Redeclaring a queue with other arguments fails with PRECONDITION_FAILED and closes the channel.
        foreach (var queue in MeterReadingsExchange.ProcessingQueues)
        {
            await channel.QueueDeclareAsync(queue, durable: true, exclusive: false, autoDelete: false, cancellationToken: ct);
        }

        channel.IsOpen.ShouldBeTrue();
    }

    [Theory]
    [InlineData(MeterReadingsExchange.MotionRoutingKey, MeterReadingsExchange.MotionQueue)]
    [InlineData(MeterReadingsExchange.EnergyRoutingKey, MeterReadingsExchange.EnergyQueue)]
    [InlineData(MeterReadingsExchange.AirQualityRoutingKey, MeterReadingsExchange.AirQualityQueue)]
    public async Task StartAsync_ExchangeExists_BindsEachQueueToItsRoutingKeyOnly(string routingKey, string expectedQueue)
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        await using var exchange = await MeterReadingsExchange.CreateAsync(rabbitMq, ct);

        await using (var factory = new ProcessorApplicationFactory(rabbitMq, postgreSql))
        {
            factory.StartServer();
            await exchange.WaitForConsumersAsync(ct);
        }

        // The processor is stopped, so routed messages stay in its queues.
        foreach (var queue in MeterReadingsExchange.ProcessingQueues)
        {
            await exchange.PurgeAsync(queue, ct);
        }

        // Act
        await exchange.PublishRawAsync(routingKey, "{}"u8.ToArray(), ct);

        // Assert
        await TestWait.UntilAsync(async () => await exchange.GetReadyMessageCountAsync(expectedQueue, ct) == 1, RoutingTimeout, ct);

        foreach (var queue in MeterReadingsExchange.ProcessingQueues.Except([expectedQueue]))
        {
            (await exchange.GetReadyMessageCountAsync(queue, ct)).ShouldBe(0u, queue);
        }

        await exchange.PurgeAsync(expectedQueue, ct);
    }
}
