using System.Collections.Concurrent;
using Ausiannikau.Utilities.Results;
using DataIngestor.Application.ExternalMeters;
using DataIngestor.Background.Configuration;
using DataIngestor.Background.Workers;
using DataIngestor.UnitTests.TestDoubles;
using DataIngestor.UnitTests.Utilities;
using FakeItEasy;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace DataIngestor.UnitTests.Background.Workers;

// The worker runs on a background task, so tests wait for its observable effects (polls, timers)
// with TestWait. Time itself only moves when a test advances the fake time provider.
public sealed class MeterPollingWorkerTests : IDisposable
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
    private readonly LogCapture _logs = new();
    private MeterPollingWorker? _sut;

    public MeterPollingWorkerTests()
    {
        A.CallTo(() => _metersService.ExtractReadingsAsync(A<CancellationToken>._))
            .Returns(Result.Succeed());

        A.CallTo(() => _scopeFactory.CreateScope())
            .ReturnsLazily(CreateScope);
    }

    public void Dispose()
    {
        _sut?.Dispose();
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
        A.CallTo(() => _metersService.ExtractReadingsAsync(A<CancellationToken>._)).MustHaveHappenedOnceExactly();
        _logs.Entries.ShouldContain(entry =>
            entry.Level == LogLevel.Information &&
            entry.Message == "Meter polling job is starting. Polling period: 60 seconds");
        _logs.Entries.ShouldNotContain(entry => entry.Message.Contains("before first attempt", StringComparison.Ordinal));
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
        A.CallTo(() => _metersService.ExtractReadingsAsync(A<CancellationToken>._)).MustHaveHappenedOnceExactly();
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
        A.CallTo(() => _metersService.ExtractReadingsAsync(A<CancellationToken>._)).MustHaveHappenedTwiceExactly();
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
        _logs.Entries.ShouldNotContain(entry => entry.Level >= LogLevel.Warning);
    }

    [Fact]
    public async Task ExecuteAsync_PollReturnsFailure_LogsReasonAndKeepsPolling()
    {
        // Arrange
        A.CallTo(() => _metersService.ExtractReadingsAsync(A<CancellationToken>._))
            .Returns(Result.Fail("Weak App API responded with failure")).Once();
        var sut = CreateSut();

        // Act
        await sut.StartAsync(TestContext.Current.CancellationToken);
        await WaitForPollsAsync(1);
        _timeProvider.Advance(PollingPeriod);
        await WaitForPollsAsync(2);
        await sut.StopAsync(TestContext.Current.CancellationToken);

        // Assert
        var error = _logs.Entries.Where(entry => entry.Level == LogLevel.Error).ShouldHaveSingleItem();
        error.Message.ShouldBe("Meter polling job failed. Reason: Weak App API responded with failure");
        A.CallTo(() => _metersService.ExtractReadingsAsync(A<CancellationToken>._)).MustHaveHappenedTwiceExactly();
    }

    [Fact]
    public async Task ExecuteAsync_PollThrows_LogsExceptionAndKeepsPolling()
    {
        // Arrange
        var exception = new InvalidOperationException("Unexpected failure");
        A.CallTo(() => _metersService.ExtractReadingsAsync(A<CancellationToken>._))
            .ThrowsAsync(exception).Once();
        var sut = CreateSut();

        // Act
        await sut.StartAsync(TestContext.Current.CancellationToken);
        await WaitForPollsAsync(1);
        _timeProvider.Advance(PollingPeriod);
        await WaitForPollsAsync(2);
        await sut.StopAsync(TestContext.Current.CancellationToken);

        // Assert
        var error = _logs.Entries.Where(entry => entry.Level == LogLevel.Error).ShouldHaveSingleItem();
        error.Message.ShouldBe("Meter polling job failed");
        error.Exception.ShouldBeSameAs(exception);
        A.CallTo(() => _metersService.ExtractReadingsAsync(A<CancellationToken>._)).MustHaveHappenedTwiceExactly();
    }

    [Fact]
    public async Task ExecuteAsync_PollThrowsCancellationNotCausedByShutdown_TreatsItAsFailureAndKeepsPolling()
    {
        // Arrange
        var exception = new TaskCanceledException("The HTTP request timed out");
        A.CallTo(() => _metersService.ExtractReadingsAsync(A<CancellationToken>._))
            .ThrowsAsync(exception).Once();
        var sut = CreateSut();

        // Act
        await sut.StartAsync(TestContext.Current.CancellationToken);
        await WaitForPollsAsync(1);
        _timeProvider.Advance(PollingPeriod);
        await WaitForPollsAsync(2);
        await sut.StopAsync(TestContext.Current.CancellationToken);

        // Assert
        var error = _logs.Entries.Where(entry => entry.Level == LogLevel.Error).ShouldHaveSingleItem();
        error.Exception.ShouldBeSameAs(exception);
        _logs.Entries.Count(entry => entry.Message == ShuttingDownMessage).ShouldBe(1);
        A.CallTo(() => _metersService.ExtractReadingsAsync(A<CancellationToken>._)).MustHaveHappenedTwiceExactly();
    }

    [Fact]
    public async Task ExecuteAsync_EachPoll_UsesNewServiceScopeAndDisposesIt()
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
        _createdScopes.Count.ShouldBe(2);
        foreach (var scope in _createdScopes)
        {
            A.CallTo(() => scope.Dispose()).MustHaveHappenedOnceExactly();
        }
    }

    [Fact]
    public async Task ExecuteAsync_AnyPoll_PassesStoppingTokenToService()
    {
        // Arrange
        var passedToken = CancellationToken.None;
        A.CallTo(() => _metersService.ExtractReadingsAsync(A<CancellationToken>._))
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
        _logs.Entries.ShouldContain(entry => entry.Level == LogLevel.Information && entry.Message == ShuttingDownMessage);
    }

    [Fact]
    public async Task ExecuteAsync_StoppedWhilePollIsInProgress_ShutsDownGracefullyWithoutReportingFailure()
    {
        // Arrange
        A.CallTo(() => _metersService.ExtractReadingsAsync(A<CancellationToken>._))
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
        _logs.Entries.ShouldContain(entry => entry.Level == LogLevel.Information && entry.Message == ShuttingDownMessage);
        _logs.Entries.ShouldNotContain(entry => entry.Level >= LogLevel.Warning);
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
        A.CallTo(() => _metersService.ExtractReadingsAsync(A<CancellationToken>._)).MustNotHaveHappened();
        _logs.Entries.ShouldContain(entry =>
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
        A.CallTo(() => _metersService.ExtractReadingsAsync(A<CancellationToken>._)).MustHaveHappenedOnceExactly();
        _logs.Entries.ShouldContain(entry =>
            entry.Level == LogLevel.Information &&
            entry.Message == "Meter polling job is starting. Polling period: 60 seconds");
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
        _logs.Entries.ShouldContain(entry => entry.Level == LogLevel.Information && entry.Message == ShuttingDownMessage);
        _logs.Entries.ShouldNotContain(entry => entry.Message.StartsWith("Meter polling job is starting", StringComparison.Ordinal));
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
            _logs.CreateLogger<MeterPollingWorker>());

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
