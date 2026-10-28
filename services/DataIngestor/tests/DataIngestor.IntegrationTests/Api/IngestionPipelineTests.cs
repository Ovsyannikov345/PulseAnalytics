using System.Net;
using DataIngestor.Contracts.Events;
using DataIngestor.IntegrationTests.Fixtures;
using DataIngestor.IntegrationTests.Utilities;
using Shouldly;
using Xunit;

namespace DataIngestor.IntegrationTests.Api;

/// <summary>
/// Runs the whole ingestor host with the polling worker: WeakApp stub -> ingestor -> RabbitMQ.
/// </summary>
public sealed class IngestionPipelineTests(RabbitMqContainerFixture rabbitMq) : IDisposable
{
    private static readonly Dictionary<string, string?> PollEverySecond = new()
    {
        ["MeterPollingWorker:StartupDelayInSeconds"] = "1",
        ["MeterPollingWorker:PollingPeriodInSeconds"] = "1"
    };

    private readonly WeakAppStub _weakApp = new();

    [Fact]
    public async Task Host_WeakAppReturnsReadings_PublishesEveryReadingOfPollWithSharedCorrelationId()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        _weakApp.RespondsWith(WeakAppStub.Readings());
        await using var queue = await MeterReadingsQueue.CreateAsync(rabbitMq, ct);
        await using var factory = new IngestorApplicationFactory(rabbitMq, _weakApp, PollEverySecond);

        // Act
        factory.StartServer();
        var messages = await queue.WaitForMessagesAsync(3, ct);

        // Assert
        var firstPoll = messages.Take(3).ToList();
        firstPoll.Select(message => message.RoutingKey).ShouldBe(
        [
            "meter-reading.motion.received",
            "meter-reading.energy.received",
            "meter-reading.air-quality.received"
        ]);
        firstPoll.Select(message => message.Properties.Type).ShouldBe(
            [nameof(MotionReadingReceived), nameof(EnergyReadingReceived), nameof(AirQualityReadingReceived)]);
        firstPoll.Select(message => message.Properties.CorrelationId).Distinct().ShouldHaveSingleItem().ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public async Task Host_WeakAppFailsThenRecovers_KeepsPollingAndPublishesAfterRecovery()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var failure = WeakAppStub.Status(HttpStatusCode.InternalServerError);

        // The first poll fails on every attempt (1 + 2 retries); the next poll succeeds.
        _weakApp.RespondsInSequence(failure, failure, failure, WeakAppStub.Readings());
        await using var queue = await MeterReadingsQueue.CreateAsync(rabbitMq, ct);
        await using var factory = new IngestorApplicationFactory(rabbitMq, _weakApp, PollEverySecond);

        // Act
        factory.StartServer();
        var messages = await queue.WaitForMessagesAsync(3, ct);

        // Assert
        messages.Take(3).Select(message => message.Properties.Type).ShouldBe(
            [nameof(MotionReadingReceived), nameof(EnergyReadingReceived), nameof(AirQualityReadingReceived)]);
        _weakApp.MetersRequests.Count.ShouldBeGreaterThanOrEqualTo(4);
    }

    [Fact]
    public async Task Ping_HostIsRunning_ReturnsPong()
    {
        // Arrange
        await using var factory = new IngestorApplicationFactory(rabbitMq, _weakApp);
        using var client = factory.CreateClient();

        // Act
        var response = await client.GetStringAsync(new Uri("/ping", UriKind.Relative), TestContext.Current.CancellationToken);

        // Assert
        response.ShouldBe("pong");
    }

    public void Dispose()
    {
        _weakApp.Dispose();
    }
}
