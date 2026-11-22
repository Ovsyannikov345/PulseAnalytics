using FakeItEasy;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using Microsoft.Extensions.Time.Testing;
using NotificationService.Application.LiveReadings;
using NotificationService.Application.LiveReadings.Enums;
using NotificationService.Application.LiveReadings.Models;
using NotificationService.Application.Telemetry.Metrics;
using Shouldly;
using Xunit;

namespace NotificationService.UnitTests.Application.LiveReadings;

public sealed class ReadingsBroadcastServiceTests
{
    private static readonly DateTimeOffset ReceivedAt = new(2026, 11, 17, 10, 15, 30, TimeSpan.Zero);

    private static readonly AirQualityLiveReadingModel Reading = new()
    {
        Id = Guid.Parse("019a91b2-4c6e-7d13-9a58-3e7f2b6c1d03"),
        Location = MeterLocation.LivingRoom,
        ReceivedAt = ReceivedAt,
        Co2 = 803,
        Pm25 = 25,
        Humidity = 36,
    };

    private readonly IReadingsNotifier _notifier = A.Fake<IReadingsNotifier>();
    private readonly FakeTimeProvider _timeProvider = new(ReceivedAt.AddMilliseconds(1250));
    private readonly IMetricsService _metrics = A.Fake<IMetricsService>();
    private readonly FakeLogger<ReadingsBroadcastService> _logger = new();
    private readonly ReadingsBroadcastService _sut;

    public ReadingsBroadcastServiceTests()
    {
        _sut = new ReadingsBroadcastService(_notifier, _timeProvider, _metrics, _logger);
    }

    [Fact]
    public async Task BroadcastAsync_Reading_NotifiesClientsAboutIt()
    {
        // Act
        await _sut.BroadcastAsync(Reading, TestContext.Current.CancellationToken);

        // Assert
        A.CallTo(() => _notifier.NotifyReadingStoredAsync(Reading, TestContext.Current.CancellationToken))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task BroadcastAsync_Reading_RecordsBroadcastWithDelaySinceItWasReceived()
    {
        // Act
        await _sut.BroadcastAsync(Reading, TestContext.Current.CancellationToken);

        // Assert
        A.CallTo(() => _metrics.RecordBroadcast(MeterType.AirQuality, TimeSpan.FromMilliseconds(1250)))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task BroadcastAsync_ReceivedAtAheadOfLocalClock_RecordsZeroDelay()
    {
        // Arrange
        var reading = Reading with { ReceivedAt = _timeProvider.GetUtcNow().AddMilliseconds(200) };

        // Act
        await _sut.BroadcastAsync(reading, TestContext.Current.CancellationToken);

        // Assert
        A.CallTo(() => _metrics.RecordBroadcast(MeterType.AirQuality, TimeSpan.Zero)).MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task BroadcastAsync_Reading_LogsItAtDebug()
    {
        // Act
        await _sut.BroadcastAsync(Reading, TestContext.Current.CancellationToken);

        // Assert
        var entry = _logger.Collector.GetSnapshot().ShouldHaveSingleItem();
        entry.Level.ShouldBe(LogLevel.Debug);
        entry.Message.ShouldStartWith($"Reading {Reading.Id} (AirQuality, LivingRoom) is broadcast");
    }

    [Fact]
    public async Task BroadcastAsync_NotifierFails_RecordsNoBroadcast()
    {
        // Arrange
        var exception = new InvalidOperationException("Connection lost");
        A.CallTo(() => _notifier.NotifyReadingStoredAsync(A<LiveReadingModel>._, A<CancellationToken>._)).ThrowsAsync(exception);

        // Act & Assert
        (await Should.ThrowAsync<InvalidOperationException>(
            () => _sut.BroadcastAsync(Reading, TestContext.Current.CancellationToken))).ShouldBeSameAs(exception);
        A.CallTo(_metrics).MustNotHaveHappened();
    }
}
