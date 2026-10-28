using System.Collections.Concurrent;
using System.Diagnostics;
using Ausiannikau.Utilities.Results;
using DataIngestor.Application.Correlation;
using DataIngestor.Application.ExternalMeters;
using DataIngestor.Application.Telemetry.Metrics;
using DataIngestor.Application.Telemetry.Metrics.Enums;
using DataIngestor.Application.Telemetry.Traces;
using DataIngestor.Background.Configuration;
using DataIngestor.Background.Workers;
using DataIngestor.UnitTests.TestDoubles;
using DataIngestor.UnitTests.Utilities;
using FakeItEasy;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace DataIngestor.UnitTests.Background.Workers;

// The worker runs on a background task, so tests wait for its observable effects (polls, timers)
// with TestWait. Time itself only moves when a test advances the fake time provider.
public sealed class MeterPollingWorkerTests : IAsyncDisposable
{
    private const int PollingPeriodInSeconds = 60;
    private const int StartupDelayInSeconds = 30;

    private const string ShuttingDownMessage = "Meter polling job is gracefully shutting down";

    private static readonly TimeSpan PollingPeriod = TimeSpan.FromSeconds(PollingPeriodInSeconds);
    private static readonly TimeSpan StartupDelay = TimeSpan.FromSeconds(StartupDelayInSeconds);

    private readonly IExternalMetersService _metersService = A.Fake<IExternalMetersService>();
    private readonly IServiceScopeFactory _scopeFactory = A.Fake<IServiceScopeFactory>();
    private readonly ConcurrentQueue<IServiceScope> _createdScopes = new();
    private readonly TimerTrackingFakeTimeProvider _timeProvider = new();
    private readonly ConcurrentQueue<(string Record, Guid CorrelationId)> _correlatedLogRecords = new();
    private readonly FakeLogger<MeterPollingWorker> _logger;
    private readonly IMetricsService _metrics = A.Fake<IMetricsService>();
    private readonly ITracingService _tracing = A.Fake<ITracingService>();
    private readonly RecordingActivitySource _pollActivities = new();
    private MeterPollingWorker? _sut;

    public MeterPollingWorkerTests()
    {
        // The output sink runs synchronously inside each log call, so it sees the correlation ID the record was written under.
        _logger = new FakeLogger<MeterPollingWorker>(FakeLogCollector.Create(new FakeLogCollectorOptions
        {
            OutputSink = record => _correlatedLogRecords.Enqueue((record, CorrelationContext.CorrelationId))
        }));

        A.CallTo(() => _metersService.IngestReadingsAsync(A<CancellationToken>._))
            .Returns(Result.Succeed());

        A.CallTo(() => _scopeFactory.CreateScope())
            .ReturnsLazily(CreateScope);

        A.CallTo(() => _tracing.StartPoll(A<Guid>._))
            .ReturnsLazily(() => _pollActivities.Start());
    }

    public enum PollBehavior
    {
        Succeeds,
        ReturnsFailure,
        Throws
    }

    // Stops the worker even when a test failed before stopping it, so its background task
    // cannot outlive the test.
    public async ValueTask DisposeAsync()
    {
        if (_sut is not null)
        {
            await _sut.StopAsync(CancellationToken.None);

            _sut.Dispose();
        }

        _pollActivities.Dispose();
    }

    [Fact]
    public async Task ExecuteAsync_NoStartupDelay_PollsImmediatelyOnStart()
    {
        // Arrange
        var sut = CreateSut();

        // Act
        await sut.StartAsync(TestContext.Current.CancellationToken);
        await WaitForPollsAsync(1);
        await sut.StopAsync(TestContext.Current.CancellationToken);

        // Assert
        A.CallTo(() => _metersService.IngestReadingsAsync(A<CancellationToken>._)).MustHaveHappenedOnceExactly();
        _logger.Collector.GetSnapshot().ShouldContain(entry =>
            entry.Level == LogLevel.Information &&
            entry.Message == "Meter polling job is starting. Polling period: 60 seconds");
        _logger.Collector.GetSnapshot().ShouldNotContain(entry => entry.Message.Contains("before first attempt", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ExecuteAsync_PollingPeriodNotYetElapsed_DoesNotPollAgain()
    {
        // Arrange
        var sut = CreateSut();

        // Act
        await sut.StartAsync(TestContext.Current.CancellationToken);
        await WaitForPollsAsync(1);
        _timeProvider.Advance(PollingPeriod - TimeSpan.FromSeconds(1));
        await sut.StopAsync(TestContext.Current.CancellationToken);

        // Assert
        A.CallTo(() => _metersService.IngestReadingsAsync(A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task ExecuteAsync_PollingPeriodElapsed_PollsAgain()
    {
        // Arrange
        var sut = CreateSut();

        // Act
        await sut.StartAsync(TestContext.Current.CancellationToken);
        await WaitForPollsAsync(1);
        _timeProvider.Advance(PollingPeriod);
        await WaitForPollsAsync(2);
        await sut.StopAsync(TestContext.Current.CancellationToken);

        // Assert
        A.CallTo(() => _metersService.IngestReadingsAsync(A<CancellationToken>._)).MustHaveHappenedTwiceExactly();
    }

    [Fact]
    public async Task ExecuteAsync_SuccessfulPoll_LogsNoWarningsOrErrors()
    {
        // Arrange
        var sut = CreateSut();

        // Act
        await sut.StartAsync(TestContext.Current.CancellationToken);
        await WaitForPollsAsync(1);
        await sut.StopAsync(TestContext.Current.CancellationToken);

        // Assert
        _logger.Collector.GetSnapshot().ShouldNotContain(entry => entry.Level >= LogLevel.Warning);
    }

    [Fact]
    public async Task ExecuteAsync_PollReturnsFailure_LogsReasonAndKeepsPolling()
    {
        // Arrange
        A.CallTo(() => _metersService.IngestReadingsAsync(A<CancellationToken>._))
            .Returns(Result.Fail("Weak App API responded with failure")).Once();
        var sut = CreateSut();

        // Act
        await sut.StartAsync(TestContext.Current.CancellationToken);
        await WaitForPollsAsync(1);
        _timeProvider.Advance(PollingPeriod);
        await WaitForPollsAsync(2);
        await sut.StopAsync(TestContext.Current.CancellationToken);

        // Assert
        var error = _logger.Collector.GetSnapshot().Where(entry => entry.Level == LogLevel.Error).ShouldHaveSingleItem();
        error.Message.ShouldBe("Meter polling job failed. Reason: Weak App API responded with failure");
        A.CallTo(() => _metersService.IngestReadingsAsync(A<CancellationToken>._)).MustHaveHappenedTwiceExactly();
    }

    [Fact]
    public async Task ExecuteAsync_PollThrows_LogsExceptionAndKeepsPolling()
    {
        // Arrange
        var exception = new InvalidOperationException("Unexpected failure");
        A.CallTo(() => _metersService.IngestReadingsAsync(A<CancellationToken>._))
            .ThrowsAsync(exception).Once();
        var sut = CreateSut();

        // Act
        await sut.StartAsync(TestContext.Current.CancellationToken);
        await WaitForPollsAsync(1);
        _timeProvider.Advance(PollingPeriod);
        await WaitForPollsAsync(2);
        await sut.StopAsync(TestContext.Current.CancellationToken);

        // Assert
        var error = _logger.Collector.GetSnapshot().Where(entry => entry.Level == LogLevel.Error).ShouldHaveSingleItem();
        error.Message.ShouldBe("Meter polling job failed");
        error.Exception.ShouldBeSameAs(exception);
        A.CallTo(() => _metersService.IngestReadingsAsync(A<CancellationToken>._)).MustHaveHappenedTwiceExactly();
    }

    [Fact]
    public async Task ExecuteAsync_PollThrowsCancellationNotCausedByShutdown_TreatsItAsFailureAndKeepsPolling()
    {
        // Arrange
        var exception = new TaskCanceledException("The HTTP request timed out");
        A.CallTo(() => _metersService.IngestReadingsAsync(A<CancellationToken>._))
            .ThrowsAsync(exception).Once();
        var sut = CreateSut();

        // Act
        await sut.StartAsync(TestContext.Current.CancellationToken);
        await WaitForPollsAsync(1);
        _timeProvider.Advance(PollingPeriod);
        await WaitForPollsAsync(2);
        await sut.StopAsync(TestContext.Current.CancellationToken);

        // Assert
        var error = _logger.Collector.GetSnapshot().Where(entry => entry.Level == LogLevel.Error).ShouldHaveSingleItem();
        error.Exception.ShouldBeSameAs(exception);
        _logger.Collector.GetSnapshot().Count(entry => entry.Message == ShuttingDownMessage).ShouldBe(1);
        A.CallTo(() => _metersService.IngestReadingsAsync(A<CancellationToken>._)).MustHaveHappenedTwiceExactly();
    }

    [Theory]
    [InlineData(PollBehavior.Succeeds)]
    [InlineData(PollBehavior.ReturnsFailure)]
    [InlineData(PollBehavior.Throws)]
    public async Task ExecuteAsync_AnyPollOutcome_UsesNewServiceScopePerPollAndDisposesIt(PollBehavior behavior)
    {
        // Arrange
        A.CallTo(() => _metersService.IngestReadingsAsync(A<CancellationToken>._))
            .ReturnsLazily(() => behavior switch
            {
                PollBehavior.Succeeds => Task.FromResult(Result.Succeed()),
                PollBehavior.ReturnsFailure => Task.FromResult(Result.Fail("Weak App API responded with failure")),
                _ => Task.FromException<Result>(new InvalidOperationException("Unexpected failure"))
            });
        var sut = CreateSut();

        // Act
        await sut.StartAsync(TestContext.Current.CancellationToken);
        await WaitForPollsAsync(1);
        _timeProvider.Advance(PollingPeriod);
        await WaitForPollsAsync(2);
        await sut.StopAsync(TestContext.Current.CancellationToken);

        // Assert
        _createdScopes.Count.ShouldBe(2);
        foreach (var scope in _createdScopes)
        {
            A.CallTo(() => scope.Dispose()).MustHaveHappenedOnceExactly();
        }
    }

    [Fact]
    public async Task ExecuteAsync_EachPoll_RunsUnderNewVersion7CorrelationIdCreatedAtPollTime()
    {
        // Arrange
        var pollCorrelationIds = new ConcurrentQueue<Guid>();
        A.CallTo(() => _metersService.IngestReadingsAsync(A<CancellationToken>._))
            .Invokes(() => pollCorrelationIds.Enqueue(CorrelationContext.CorrelationId))
            .Returns(Result.Succeed());
        var firstPollTime = _timeProvider.GetUtcNow();
        var sut = CreateSut();

        // Act
        await sut.StartAsync(TestContext.Current.CancellationToken);
        await WaitForPollsAsync(1);
        _timeProvider.Advance(PollingPeriod);
        await WaitForPollsAsync(2);
        await sut.StopAsync(TestContext.Current.CancellationToken);

        // Assert
        pollCorrelationIds.Count.ShouldBe(2);
        var (first, second) = (pollCorrelationIds.First(), pollCorrelationIds.Last());
        first.Version.ShouldBe(7);
        second.Version.ShouldBe(7);
        second.ShouldNotBe(first);
        GuidVersion7.GetTimestamp(first).ShouldBe(firstPollTime);
        GuidVersion7.GetTimestamp(second).ShouldBe(firstPollTime + PollingPeriod);
    }

    [Theory]
    [InlineData(PollBehavior.ReturnsFailure)]
    [InlineData(PollBehavior.Throws)]
    public async Task ExecuteAsync_PollFails_LogsFailureUnderPollCorrelationId(PollBehavior behavior)
    {
        // Arrange
        var pollCorrelationId = Guid.Empty;
        A.CallTo(() => _metersService.IngestReadingsAsync(A<CancellationToken>._))
            .ReturnsLazily(() =>
            {
                pollCorrelationId = CorrelationContext.CorrelationId;

                return behavior == PollBehavior.ReturnsFailure
                    ? Task.FromResult(Result.Fail("Weak App API responded with failure"))
                    : Task.FromException<Result>(new InvalidOperationException("Unexpected failure"));
            });
        var sut = CreateSut();

        // Act
        await sut.StartAsync(TestContext.Current.CancellationToken);
        await WaitForPollsAsync(1);
        await sut.StopAsync(TestContext.Current.CancellationToken);

        // Assert
        var failure = _correlatedLogRecords
            .Where(entry => entry.Record.Contains("Meter polling job failed", StringComparison.Ordinal))
            .ShouldHaveSingleItem();
        failure.CorrelationId.ShouldBe(pollCorrelationId);
    }

    [Fact]
    public async Task ExecuteAsync_StoppedAfterPoll_DoesNotLogShutdownUnderPollCorrelationId()
    {
        // Arrange
        var pollCorrelationId = Guid.Empty;
        A.CallTo(() => _metersService.IngestReadingsAsync(A<CancellationToken>._))
            .Invokes(() => pollCorrelationId = CorrelationContext.CorrelationId)
            .Returns(Result.Succeed());
        var sut = CreateSut();

        // Act
        await sut.StartAsync(TestContext.Current.CancellationToken);
        await WaitForPollsAsync(1);
        await sut.StopAsync(TestContext.Current.CancellationToken);

        // Assert
        var shutdown = _correlatedLogRecords
            .Where(entry => entry.Record.Contains(ShuttingDownMessage, StringComparison.Ordinal))
            .ShouldHaveSingleItem();
        shutdown.CorrelationId.ShouldNotBe(pollCorrelationId);
    }

    [Fact]
    public async Task ExecuteAsync_AnyPoll_PassesStoppingTokenToService()
    {
        // Arrange
        var passedToken = CancellationToken.None;
        A.CallTo(() => _metersService.IngestReadingsAsync(A<CancellationToken>._))
            .Invokes((CancellationToken token) => passedToken = token)
            .Returns(Result.Succeed());
        var sut = CreateSut();

        // Act
        await sut.StartAsync(TestContext.Current.CancellationToken);
        await WaitForPollsAsync(1);
        var cancelledBeforeStop = passedToken.IsCancellationRequested;
        await sut.StopAsync(TestContext.Current.CancellationToken);

        // Assert
        passedToken.CanBeCanceled.ShouldBeTrue();
        cancelledBeforeStop.ShouldBeFalse();
        passedToken.IsCancellationRequested.ShouldBeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_StoppedBetweenPolls_ShutsDownGracefully()
    {
        // Arrange
        var sut = CreateSut();

        // Act
        await sut.StartAsync(TestContext.Current.CancellationToken);
        await WaitForPollsAsync(1);
        await sut.StopAsync(TestContext.Current.CancellationToken);

        // Assert
        sut.ExecuteTask.ShouldNotBeNull().IsCanceled.ShouldBeTrue();
        _logger.Collector.GetSnapshot().ShouldContain(entry => entry.Level == LogLevel.Information && entry.Message == ShuttingDownMessage);
    }

    [Fact]
    public async Task ExecuteAsync_StoppedWhilePollIsInProgress_ShutsDownGracefullyWithoutReportingFailure()
    {
        // Arrange
        A.CallTo(() => _metersService.IngestReadingsAsync(A<CancellationToken>._))
            .ReturnsLazily(async (CancellationToken token) =>
            {
                await Task.Delay(Timeout.Infinite, token);

                return Result.Succeed();
            });
        var sut = CreateSut();

        // Act
        await sut.StartAsync(TestContext.Current.CancellationToken);
        await WaitForPollsAsync(1);
        await sut.StopAsync(TestContext.Current.CancellationToken);

        // Assert
        sut.ExecuteTask.ShouldNotBeNull().IsCanceled.ShouldBeTrue();
        _logger.Collector.GetSnapshot().ShouldContain(entry => entry.Level == LogLevel.Information && entry.Message == ShuttingDownMessage);
        _logger.Collector.GetSnapshot().ShouldNotContain(entry => entry.Level >= LogLevel.Warning);
    }

    [Fact]
    public async Task ExecuteAsync_StoppedWhilePollIsInProgress_DisposesServiceScope()
    {
        // Arrange
        A.CallTo(() => _metersService.IngestReadingsAsync(A<CancellationToken>._))
            .ReturnsLazily(async (CancellationToken token) =>
            {
                await Task.Delay(Timeout.Infinite, token);

                return Result.Succeed();
            });
        var sut = CreateSut();

        // Act
        await sut.StartAsync(TestContext.Current.CancellationToken);
        await WaitForPollsAsync(1);
        await sut.StopAsync(TestContext.Current.CancellationToken);

        // Assert
        var scope = _createdScopes.ShouldHaveSingleItem();
        A.CallTo(() => scope.Dispose()).MustHaveHappenedOnceExactly();
    }

    [Theory]
    [InlineData(PollBehavior.Succeeds, PollOutcome.Succeeded)]
    [InlineData(PollBehavior.ReturnsFailure, PollOutcome.Failed)]
    [InlineData(PollBehavior.Throws, PollOutcome.Failed)]
    public async Task ExecuteAsync_PollCompletes_RecordsPollDurationWithOutcome(PollBehavior behavior, PollOutcome expectedOutcome)
    {
        // Arrange
        A.CallTo(() => _metersService.IngestReadingsAsync(A<CancellationToken>._))
            .ReturnsLazily(() =>
            {
                _timeProvider.Advance(TimeSpan.FromSeconds(3));

                return behavior switch
                {
                    PollBehavior.Succeeds => Task.FromResult(Result.Succeed()),
                    PollBehavior.ReturnsFailure => Task.FromResult(Result.Fail("Weak App API responded with failure")),
                    _ => Task.FromException<Result>(new InvalidOperationException("Unexpected failure"))
                };
            });
        var sut = CreateSut();

        // Act
        await sut.StartAsync(TestContext.Current.CancellationToken);
        await WaitForPollsAsync(1);
        await sut.StopAsync(TestContext.Current.CancellationToken);

        // Assert
        A.CallTo(() => _metrics.RecordPoll(A<TimeSpan>._, A<PollOutcome>._)).MustHaveHappenedOnceExactly();
        A.CallTo(() => _metrics.RecordPoll(TimeSpan.FromSeconds(3), expectedOutcome)).MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task ExecuteAsync_StoppedWhilePollIsInProgress_DoesNotRecordPollDuration()
    {
        // Arrange
        A.CallTo(() => _metersService.IngestReadingsAsync(A<CancellationToken>._))
            .ReturnsLazily(async (CancellationToken token) =>
            {
                await Task.Delay(Timeout.Infinite, token);

                return Result.Succeed();
            });
        var sut = CreateSut();

        // Act
        await sut.StartAsync(TestContext.Current.CancellationToken);
        await WaitForPollsAsync(1);
        await sut.StopAsync(TestContext.Current.CancellationToken);

        // Assert
        A.CallTo(() => _metrics.RecordPoll(A<TimeSpan>._, A<PollOutcome>._)).MustNotHaveHappened();
    }

    [Fact]
    public async Task ExecuteAsync_EachPoll_RunsPollInsideItsOwnSpanStartedForPollCorrelationId()
    {
        // Arrange
        var pollCorrelationIds = new ConcurrentQueue<Guid>();
        var currentActivities = new ConcurrentQueue<Activity?>();
        A.CallTo(() => _metersService.IngestReadingsAsync(A<CancellationToken>._))
            .Invokes(() =>
            {
                pollCorrelationIds.Enqueue(CorrelationContext.CorrelationId);
                currentActivities.Enqueue(Activity.Current);
            })
            .Returns(Result.Succeed());
        var sut = CreateSut();

        // Act
        await sut.StartAsync(TestContext.Current.CancellationToken);
        await WaitForPollsAsync(1);
        _timeProvider.Advance(PollingPeriod);
        await WaitForPollsAsync(2);
        await sut.StopAsync(TestContext.Current.CancellationToken);

        // Assert
        Fake.GetCalls(_tracing)
            .Select(call => call.GetArgument<Guid>("correlationId"))
            .ShouldBe(pollCorrelationIds);
        _pollActivities.StoppedActivities.Count.ShouldBe(2);
        _pollActivities.StoppedActivities.ShouldAllBe(activity => activity.Status == ActivityStatusCode.Unset);
        currentActivities.ShouldBe(_pollActivities.StoppedActivities.Cast<Activity?>());
    }

    [Theory]
    [InlineData(PollBehavior.ReturnsFailure)]
    [InlineData(PollBehavior.Throws)]
    public async Task ExecuteAsync_PollFails_MarksPollActivityAsError(PollBehavior behavior)
    {
        // Arrange
        var exception = new InvalidOperationException("Unexpected failure");
        A.CallTo(() => _metersService.IngestReadingsAsync(A<CancellationToken>._))
            .ReturnsLazily(() => behavior == PollBehavior.ReturnsFailure
                ? Task.FromResult(Result.Fail("Weak App API responded with failure"))
                : Task.FromException<Result>(exception));
        var sut = CreateSut();

        // Act
        await sut.StartAsync(TestContext.Current.CancellationToken);
        await WaitForPollsAsync(1);
        await sut.StopAsync(TestContext.Current.CancellationToken);

        // Assert
        var activity = _pollActivities.StoppedActivities.ShouldHaveSingleItem();
        activity.Status.ShouldBe(ActivityStatusCode.Error);
        if (behavior == PollBehavior.ReturnsFailure)
        {
            activity.StatusDescription.ShouldBe("Weak App API responded with failure");
        }
        else
        {
            activity.StatusDescription.ShouldBe(exception.Message);
            activity.Events.ShouldContain(activityEvent => activityEvent.Name == "exception");
            activity.GetTagItem("error.type").ShouldBe(typeof(InvalidOperationException).FullName);
        }
    }

    [Fact]
    public async Task ExecuteAsync_StartupDelayNotYetElapsed_DoesNotPoll()
    {
        // Arrange
        var sut = CreateSut(StartupDelayInSeconds);

        // Act
        await sut.StartAsync(TestContext.Current.CancellationToken);
        await WaitForTimersAsync(1);
        _timeProvider.Advance(StartupDelay - TimeSpan.FromSeconds(1));
        await sut.StopAsync(TestContext.Current.CancellationToken);

        // Assert
        A.CallTo(() => _metersService.IngestReadingsAsync(A<CancellationToken>._)).MustNotHaveHappened();
        _logger.Collector.GetSnapshot().ShouldContain(entry =>
            entry.Level == LogLevel.Information &&
            entry.Message == "Meter polling job is waiting 30 seconds before first attempt");
    }

    [Fact]
    public async Task ExecuteAsync_StartupDelayElapsed_StartsPolling()
    {
        // Arrange
        var sut = CreateSut(StartupDelayInSeconds);

        // Act
        await sut.StartAsync(TestContext.Current.CancellationToken);
        await WaitForTimersAsync(1);
        _timeProvider.Advance(StartupDelay);
        await WaitForPollsAsync(1);
        await sut.StopAsync(TestContext.Current.CancellationToken);

        // Assert
        A.CallTo(() => _metersService.IngestReadingsAsync(A<CancellationToken>._)).MustHaveHappenedOnceExactly();
        _logger.Collector.GetSnapshot().ShouldContain(entry =>
            entry.Level == LogLevel.Information &&
            entry.Message == "Meter polling job is starting. Polling period: 60 seconds");
    }

    [Fact]
    public async Task ExecuteAsync_StartupDelayElapsed_CountsPollingPeriodFromFirstPoll()
    {
        // Arrange
        var sut = CreateSut(StartupDelayInSeconds);

        // Act
        await sut.StartAsync(TestContext.Current.CancellationToken);
        await WaitForTimersAsync(1);
        _timeProvider.Advance(StartupDelay);
        await WaitForPollsAsync(1);
        _timeProvider.Advance(PollingPeriod - TimeSpan.FromSeconds(1));
        await sut.StopAsync(TestContext.Current.CancellationToken);

        // Assert
        A.CallTo(() => _metersService.IngestReadingsAsync(A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task ExecuteAsync_StoppedDuringStartupDelay_ShutsDownGracefully()
    {
        // Arrange
        var sut = CreateSut(StartupDelayInSeconds);

        // Act
        await sut.StartAsync(TestContext.Current.CancellationToken);
        await WaitForTimersAsync(1);
        await sut.StopAsync(TestContext.Current.CancellationToken);

        // Assert
        sut.ExecuteTask.ShouldNotBeNull().IsCanceled.ShouldBeTrue();
        _logger.Collector.GetSnapshot().ShouldContain(entry => entry.Level == LogLevel.Information && entry.Message == ShuttingDownMessage);
        _logger.Collector.GetSnapshot().ShouldNotContain(entry => entry.Message.StartsWith("Meter polling job is starting", StringComparison.Ordinal));
    }

    private MeterPollingWorker CreateSut(int? startupDelayInSeconds = null)
    {
        var settings = new MeterPollingWorkerSettings
        {
            PollingPeriodInSeconds = PollingPeriodInSeconds,
            StartupDelayInSeconds = startupDelayInSeconds
        };

        _sut = new MeterPollingWorker(
            _scopeFactory,
            Options.Create(settings),
            _timeProvider,
            _metrics,
            _tracing,
            _logger);

        return _sut;
    }

    private IServiceScope CreateScope()
    {
        var serviceProvider = A.Fake<IServiceProvider>();
        A.CallTo(() => serviceProvider.GetService(typeof(IExternalMetersService))).Returns(_metersService);

        var scope = A.Fake<IServiceScope>();
        A.CallTo(() => scope.ServiceProvider).Returns(serviceProvider);

        _createdScopes.Enqueue(scope);

        return scope;
    }

    private Task WaitForPollsAsync(int count)
    {
        return TestWait.UntilAsync(
            () => Fake.GetCalls(_metersService).Count() >= count,
            TestContext.Current.CancellationToken);
    }

    private Task WaitForTimersAsync(int count)
    {
        return TestWait.UntilAsync(
            () => _timeProvider.CreatedTimersCount >= count,
            TestContext.Current.CancellationToken);
    }
}
