using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using Ausiannikau.Utilities.Results;
using DataIngestor.Application.Correlation;
using DataIngestor.Application.Interfaces.Messaging;
using DataIngestor.Contracts.Events;
using DataIngestor.Domain.Entities;
using DataIngestor.IntegrationTests.Fixtures;
using DataIngestor.IntegrationTests.Utilities;
using Microsoft.Extensions.DependencyInjection;
using RabbitMQ.Client;
using Shouldly;
using Xunit;

namespace DataIngestor.IntegrationTests.Infrastructure.RabbitMq;

public sealed class MeterReadingsPublisherTests(RabbitMqContainerFixture rabbitMq) : IDisposable
{
    private readonly WeakAppStub _weakApp = new();

    [Fact]
    public async Task PublishAsync_ReadingsOfAllTypes_RoutesEachToItsRoutingKey()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        await using var motionQueue = await MeterReadingsQueue.CreateAsync(rabbitMq, ct, "meter-reading.motion.received");
        await using var energyQueue = await MeterReadingsQueue.CreateAsync(rabbitMq, ct, "meter-reading.energy.received");
        await using var airQualityQueue = await MeterReadingsQueue.CreateAsync(rabbitMq, ct, "meter-reading.air-quality.received");
        await using var factory = new IngestorApplicationFactory(rabbitMq, _weakApp);

        // Act
        var result = await PublishAsync(factory, [MeterReadings.Motion(), MeterReadings.Energy(), MeterReadings.AirQuality()]);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        (await motionQueue.WaitForMessagesAsync(1, ct)).ShouldHaveSingleItem().Properties.Type.ShouldBe(nameof(MotionReadingReceived));
        (await energyQueue.WaitForMessagesAsync(1, ct)).ShouldHaveSingleItem().Properties.Type.ShouldBe(nameof(EnergyReadingReceived));
        (await airQualityQueue.WaitForMessagesAsync(1, ct)).ShouldHaveSingleItem().Properties.Type.ShouldBe(nameof(AirQualityReadingReceived));
    }

    [Fact]
    public async Task PublishAsync_AnyReading_DeliversPersistentJsonMessageWithMetadata()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        await using var queue = await MeterReadingsQueue.CreateAsync(rabbitMq, ct);
        await using var factory = new IngestorApplicationFactory(rabbitMq, _weakApp);

        var correlationId = Guid.CreateVersion7();
        CorrelationContext.CorrelationId = correlationId;

        // Act
        await PublishAsync(factory, [MeterReadings.Energy()]);

        // Assert
        var message = (await queue.WaitForMessagesAsync(1, ct)).ShouldHaveSingleItem();
        var publishedEvent = JsonSerializer.Deserialize<EnergyReadingReceived>(message.Body, JsonSerializerOptions.Web)!;
        message.Properties.DeliveryMode.ShouldBe(DeliveryModes.Persistent);
        message.Properties.ContentType.ShouldBe("application/json");
        message.Properties.AppId.ShouldBe("data-ingestor");
        message.Properties.Type.ShouldBe(nameof(EnergyReadingReceived));
        message.Properties.MessageId.ShouldBe(publishedEvent.MessageId.ToString());
        message.Properties.CorrelationId.ShouldBe(correlationId.ToString());
    }

    [Fact]
    public async Task PublishAsync_AnyReading_DeliversBodyReadableAsContractEvent()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        await using var queue = await MeterReadingsQueue.CreateAsync(rabbitMq, ct);
        await using var factory = new IngestorApplicationFactory(rabbitMq, _weakApp);

        // Act
        await PublishAsync(factory, [MeterReadings.AirQuality()]);

        // Assert
        var message = (await queue.WaitForMessagesAsync(1, ct)).ShouldHaveSingleItem();
        var publishedEvent = JsonSerializer.Deserialize<AirQualityReadingReceived>(message.Body, JsonSerializerOptions.Web);
        publishedEvent.ShouldBe(new AirQualityReadingReceived
        {
            MessageId = publishedEvent!.MessageId,
            Location = "Kitchen",
            ReceivedAt = MeterReadings.ReceivedAt,
            Co2 = 803,
            Pm25 = 25,
            Humidity = 36
        });
    }

    [Fact]
    public async Task PublishAsync_ExchangeDoesNotExist_DeclaresDurableTopicExchange()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        await DeleteExchangeAsync(ct);
        await using var factory = new IngestorApplicationFactory(rabbitMq, _weakApp);

        // Act
        await PublishAsync(factory, [MeterReadings.Motion()]);

        // Assert
        await using var connection = await rabbitMq.CreateConnectionAsync(ct);
        await using var channel = await connection.CreateChannelAsync(cancellationToken: ct);

        // A passive declare fails if the exchange is missing; redeclaring it with other arguments fails with PRECONDITION_FAILED.
        await channel.ExchangeDeclarePassiveAsync(MeterReadingsQueue.ExchangeName, ct);
        await channel.ExchangeDeclareAsync(MeterReadingsQueue.ExchangeName, ExchangeType.Topic, durable: true, autoDelete: false, cancellationToken: ct);
        channel.IsOpen.ShouldBeTrue();
    }

    [Fact]
    public async Task PublishAsync_NoQueueIsBound_ReturnsFailureForReturnedMessages()
    {
        // Arrange
        await DeleteExchangeAsync(TestContext.Current.CancellationToken);
        await using var factory = new IngestorApplicationFactory(rabbitMq, _weakApp);

        // Act
        var result = await PublishAsync(factory, [MeterReadings.Motion(), MeterReadings.Energy()]);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.ErrorMessage.ShouldBe("Failed to publish 2 of 2 meter readings");
    }

    [Fact]
    public async Task PublishAsync_BrokerUnreachable_ReturnsConnectionFailure()
    {
        // Arrange
        await using var factory = new IngestorApplicationFactory(
            rabbitMq,
            _weakApp,
            new Dictionary<string, string?> { ["RabbitMq:Port"] = GetUnusedPort().ToString(CultureInfo.InvariantCulture) });

        // Act
        var result = await PublishAsync(factory, [MeterReadings.Motion()]);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.ErrorMessage.ShouldBe("Failed to connect to RabbitMQ");
    }

    public void Dispose()
    {
        CorrelationContext.Reset();
        _weakApp.Dispose();
    }

    private static async Task<Result> PublishAsync(IngestorApplicationFactory factory, IReadOnlyCollection<MeterReading> readings)
    {
        await using var scope = factory.Services.CreateAsyncScope();

        var publisher = scope.ServiceProvider.GetRequiredService<IMeterReadingsPublisher>();

        return await publisher.PublishAsync(readings, TestContext.Current.CancellationToken);
    }

    private static int GetUnusedPort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();

        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }

    // Deleting the exchange also drops every binding left on it, so nothing receives the next publish.
    private async Task DeleteExchangeAsync(CancellationToken ct)
    {
        await using var connection = await rabbitMq.CreateConnectionAsync(ct);
        await using var channel = await connection.CreateChannelAsync(cancellationToken: ct);

        await channel.ExchangeDeleteAsync(MeterReadingsQueue.ExchangeName, cancellationToken: ct);
    }
}
