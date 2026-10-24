using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using Ausiannikau.Utilities.Results;
using DataIngestor.Application.ExternalMeters;
using DataIngestor.Background;
using DataIngestor.UnitTests.TestDoubles;
using FakeItEasy;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Shouldly;
using Xunit;

namespace DataIngestor.UnitTests.Background.Workers;

// MeterPollingWorker is internal, so it is built through the public AddBackgroundServices
// registration and driven through the BackgroundService start/stop API, as the host would do.
// The worker uses a real PeriodicTimer, so tests that need a second poll take about a second.
public sealed class MeterPollingWorkerTests : IDisposable
{
    private const int LongPollingPeriodInSeconds = 60;
    private const int ShortPollingPeriodInSeconds = 1;

    private const string ShuttingDownMessage = "Meter polling job is gracefully shutting down";

    private readonly IExternalMetersService _metersService = A.Fake<IExternalMetersService>();
    private readonly CapturingLoggerProvider _logs = new();
    private ServiceProvider? _serviceProvider;

    public MeterPollingWorkerTests()
    {
        A.CallTo(() => _metersService.ExtractReadingsAsync(A<CancellationToken>._))
            .Returns(Result.Succeed());
    }

    public void Dispose()
    {
        _serviceProvider?.Dispose();
    }

    [Fact]
    public async Task ExecuteAsync_NoStartupDelay_PollsImmediatelyOnStart()
    {
        // Arrange
        var sut = CreateSut(LongPollingPeriodInSeconds);

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
    public async Task ExecuteAsync_PollingPeriodElapses_PollsAgain()
    {
        // Arrange
        var pollTimes = new ConcurrentQueue<TimeSpan>();
        var stopwatch = Stopwatch.StartNew();
        A.CallTo(() => _metersService.ExtractReadingsAsync(A<CancellationToken>._))
            .Invokes(() => pollTimes.Enqueue(stopwatch.Elapsed))
            .Returns(Result.Succeed());
        var sut = CreateSut(ShortPollingPeriodInSeconds);

        // Act
        await sut.StartAsync(TestContext.Current.CancellationToken);
        await WaitForPollsAsync(2);
        await sut.StopAsync(TestContext.Current.CancellationToken);

        // Assert
        var times = pollTimes.ToArray();
        times.Length.ShouldBeGreaterThanOrEqualTo(2);
        (times[1] - times[0]).ShouldBeGreaterThan(TimeSpan.FromMilliseconds(900));
    }

    [Fact]
    public async Task ExecuteAsync_SuccessfulPoll_LogsNoErrors()
    {
        // Arrange
        var sut = CreateSut(LongPollingPeriodInSeconds);

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
        var sut = CreateSut(ShortPollingPeriodInSeconds);

        // Act
        await sut.StartAsync(TestContext.Current.CancellationToken);
        await WaitForPollsAsync(2);
        await sut.StopAsync(TestContext.Current.CancellationToken);

        // Assert
        var error = _logs.Entries.Where(entry => entry.Level == LogLevel.Error).ShouldHaveSingleItem();
        error.Message.ShouldBe("Meter polling job failed. Reason: Weak App API responded with failure");
        A.CallTo(() => _metersService.ExtractReadingsAsync(A<CancellationToken>._)).MustHaveHappenedTwiceOrMore();
    }

    [Fact]
    public async Task ExecuteAsync_PollThrows_LogsExceptionAndKeepsPolling()
    {
        // Arrange
        var exception = new InvalidOperationException("Unexpected failure");
        A.CallTo(() => _metersService.ExtractReadingsAsync(A<CancellationToken>._))
            .ThrowsAsync(exception).Once();
        var sut = CreateSut(ShortPollingPeriodInSeconds);

        // Act
        await sut.StartAsync(TestContext.Current.CancellationToken);
        await WaitForPollsAsync(2);
        await sut.StopAsync(TestContext.Current.CancellationToken);

        // Assert
        var error = _logs.Entries.Where(entry => entry.Level == LogLevel.Error).ShouldHaveSingleItem();
        error.Message.ShouldBe("Meter polling job failed");
        error.Exception.ShouldBeSameAs(exception);
        A.CallTo(() => _metersService.ExtractReadingsAsync(A<CancellationToken>._)).MustHaveHappenedTwiceOrMore();
    }

    [Fact]
    public async Task ExecuteAsync_PollThrowsCancellationNotCausedByShutdown_TreatsItAsFailureAndKeepsPolling()
    {
        // Arrange
        var exception = new TaskCanceledException("The HTTP request timed out");
        A.CallTo(() => _metersService.ExtractReadingsAsync(A<CancellationToken>._))
            .ThrowsAsync(exception).Once();
        var sut = CreateSut(ShortPollingPeriodInSeconds);

        // Act
        await sut.StartAsync(TestContext.Current.CancellationToken);
        await WaitForPollsAsync(2);
        await sut.StopAsync(TestContext.Current.CancellationToken);

        // Assert
        var error = _logs.Entries.Where(entry => entry.Level == LogLevel.Error).ShouldHaveSingleItem();
        error.Exception.ShouldBeSameAs(exception);
        A.CallTo(() => _metersService.ExtractReadingsAsync(A<CancellationToken>._)).MustHaveHappenedTwiceOrMore();
    }

    [Fact]
    public async Task ExecuteAsync_EachPoll_ResolvesServiceFromNewScopeAndDisposesIt()
    {
        // Arrange
        var resolvedServices = new ConcurrentQueue<IExternalMetersService>();
        var sut = CreateSut(ShortPollingPeriodInSeconds, configureServices: services => services.AddScoped(_ =>
        {
            var service = A.Fake<IExternalMetersService>(options => options.Implements<IAsyncDisposable>());
            A.CallTo(() => service.ExtractReadingsAsync(A<CancellationToken>._)).Returns(Result.Succeed());
            resolvedServices.Enqueue(service);

            return service;
        }));

        // Act
        await sut.StartAsync(TestContext.Current.CancellationToken);
        await TestWait.UntilAsync(() => resolvedServices.Count >= 2, TestContext.Current.CancellationToken);
        await sut.StopAsync(TestContext.Current.CancellationToken);

        // Assert
        var services = resolvedServices.ToArray();
        services.Distinct().Count().ShouldBe(services.Length);
        services.ShouldAllBe(service => Fake.GetCalls(service).Any(call => call.Method.Name == nameof(IAsyncDisposable.DisposeAsync)));
    }

    [Fact]
    public async Task ExecuteAsync_AnyPoll_PassesStoppingTokenToService()
    {
        // Arrange
        var passedToken = CancellationToken.None;
        A.CallTo(() => _metersService.ExtractReadingsAsync(A<CancellationToken>._))
            .Invokes((CancellationToken token) => passedToken = token)
            .Returns(Result.Succeed());
        var sut = CreateSut(LongPollingPeriodInSeconds);

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
        var sut = CreateSut(LongPollingPeriodInSeconds);

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
        var sut = CreateSut(LongPollingPeriodInSeconds);

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
    public async Task ExecuteAsync_StartupDelayConfigured_LogsDelayAndPollsOnlyAfterIt()
    {
        // Arrange
        var firstPollAt = TimeSpan.Zero;
        var stopwatch = Stopwatch.StartNew();
        A.CallTo(() => _metersService.ExtractReadingsAsync(A<CancellationToken>._))
            .Invokes(() => firstPollAt = stopwatch.Elapsed)
            .Returns(Result.Succeed());
        var sut = CreateSut(LongPollingPeriodInSeconds, startupDelayInSeconds: 1);

        // Act
        await sut.StartAsync(TestContext.Current.CancellationToken);
        await WaitForPollsAsync(1);
        await sut.StopAsync(TestContext.Current.CancellationToken);

        // Assert
        firstPollAt.ShouldBeGreaterThan(TimeSpan.FromMilliseconds(900));
        _logs.Entries.ShouldContain(entry =>
            entry.Level == LogLevel.Information &&
            entry.Message == "Meter polling job is waiting 1 seconds before first attempt");
    }

    [Fact]
    public async Task ExecuteAsync_StoppedDuringStartupDelay_ShutsDownWithoutPolling()
    {
        // Arrange
        var sut = CreateSut(LongPollingPeriodInSeconds, startupDelayInSeconds: 3600);

        // Act
        await sut.StartAsync(TestContext.Current.CancellationToken);
        await TestWait.UntilAsync(
            () => _logs.Entries.Any(entry => entry.Message.Contains("before first attempt", StringComparison.Ordinal)),
            TestContext.Current.CancellationToken);
        await sut.StopAsync(TestContext.Current.CancellationToken);

        // Assert
        sut.ExecuteTask.ShouldNotBeNull().IsCanceled.ShouldBeTrue();
        A.CallTo(() => _metersService.ExtractReadingsAsync(A<CancellationToken>._)).MustNotHaveHappened();
        _logs.Entries.ShouldContain(entry => entry.Level == LogLevel.Information && entry.Message == ShuttingDownMessage);
        _logs.Entries.ShouldNotContain(entry => entry.Message.StartsWith("Meter polling job is starting", StringComparison.Ordinal));
    }

    private BackgroundService CreateSut(
        int pollingPeriodInSeconds,
        int? startupDelayInSeconds = null,
        Action<IServiceCollection>? configureServices = null)
    {
        var settings = new Dictionary<string, string?>
        {
            ["MeterPollingWorker:PollingPeriodInSeconds"] = pollingPeriodInSeconds.ToString(CultureInfo.InvariantCulture)
        };

        if (startupDelayInSeconds is not null)
        {
            settings["MeterPollingWorker:StartupDelayInSeconds"] = startupDelayInSeconds.Value.ToString(CultureInfo.InvariantCulture);
        }

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(settings)
            .Build();

        var services = new ServiceCollection();

        services.AddLogging(logging => logging.AddProvider(_logs.Provider));
        services.AddBackgroundServices(configuration);
        services.AddScoped(_ => _metersService);
        configureServices?.Invoke(services);

        _serviceProvider = services.BuildServiceProvider();

        return _serviceProvider.GetServices<IHostedService>().OfType<BackgroundService>().Single();
    }

    private Task WaitForPollsAsync(int count)
    {
        return TestWait.UntilAsync(
            () => Fake.GetCalls(_metersService).Count() >= count,
            TestContext.Current.CancellationToken);
    }
}
