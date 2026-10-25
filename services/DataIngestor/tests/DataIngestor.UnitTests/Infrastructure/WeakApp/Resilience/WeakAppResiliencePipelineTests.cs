using System.Diagnostics;
using System.Net;
using DataIngestor.Infrastructure.WeakApp.Configuration;
using DataIngestor.Infrastructure.WeakApp.Resilience;
using DataIngestor.UnitTests.TestDoubles;
using FakeItEasy;
using FakeItEasy.Core;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Time.Testing;
using Polly;
using Polly.Timeout;
using Shouldly;
using Xunit;

namespace DataIngestor.UnitTests.Infrastructure.WeakApp.Resilience;

public sealed class WeakAppResiliencePipelineTests : IDisposable
{
    private static readonly TimeSpan TimeStep = TimeSpan.FromMilliseconds(500);
    private static readonly TimeSpan RealTimeLimit = TimeSpan.FromSeconds(10);

    private static readonly WeakAppResilienceSettings Settings = new()
    {
        TotalTimeoutInSeconds = 20,
        AttemptTimeoutInSeconds = 5,
        MaxRetryAttempts = 2,
        RetryBaseDelayInSeconds = 1
    };

    private readonly HttpMessageHandler _handler = A.Fake<HttpMessageHandler>();
    private readonly FakeTimeProvider _timeProvider = new();
    private HttpClient _httpClient;

    public WeakAppResiliencePipelineTests()
    {
        _httpClient = CreateHttpClient(Settings);
    }

    public void Dispose()
    {
        _httpClient.Dispose();
    }

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.BadGateway)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    [InlineData(HttpStatusCode.GatewayTimeout)]
    [InlineData(HttpStatusCode.RequestTimeout)]
    public async Task Send_TransientFailureThenSuccess_RetriesAndReturnsSuccess(HttpStatusCode statusCode)
    {
        // Arrange
        _handler.CallsToSendAsync()
            .ReturnsLazily(call => Respond(call, statusCode)).Once()
            .Then.ReturnsLazily(call => Respond(call, HttpStatusCode.OK));

        // Act
        using var response = await SendAsync(TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        _handler.CallsToSendAsync().MustHaveHappenedTwiceExactly();
    }

    [Fact]
    public async Task Send_TransientFailureOnEveryAttempt_ReturnsLastFailureAfterAllRetries()
    {
        // Arrange
        _handler.RespondsWith(HttpStatusCode.GatewayTimeout, "Gateway Timeout");

        // Act
        using var response = await SendAsync(TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.GatewayTimeout);
        _handler.CallsToSendAsync().MustHaveHappened(1 + Settings.MaxRetryAttempts, Times.Exactly);
    }

    [Fact]
    public async Task Send_RequestThrowsHttpRequestExceptionThenSucceeds_RetriesAndReturnsSuccess()
    {
        // Arrange
        _handler.CallsToSendAsync()
            .ThrowsAsync(new HttpRequestException("Connection refused")).Once()
            .Then.ReturnsLazily(call => Respond(call, HttpStatusCode.OK));

        // Act
        using var response = await SendAsync(TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        _handler.CallsToSendAsync().MustHaveHappenedTwiceExactly();
    }

    [Theory]
    [InlineData(HttpStatusCode.TooManyRequests)]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.NotFound)]
    public async Task Send_NonRetriableStatusCode_ReturnsItWithoutRetrying(HttpStatusCode statusCode)
    {
        // Arrange
        _handler.RespondsWith(statusCode, "Failure");

        // Act
        using var response = await SendAsync(TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(statusCode);
        _handler.CallsToSendAsync().MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task Send_AttemptHangsThenNextSucceeds_TimesOutAttemptAndRetries()
    {
        // Arrange
        _handler.CallsToSendAsync()
            .ReturnsLazily(call => HangAsync(call)).Once()
            .Then.ReturnsLazily(call => Respond(call, HttpStatusCode.OK));

        // Act
        using var response = await SendAsync(TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        _handler.CallsToSendAsync().MustHaveHappenedTwiceExactly();
    }

    [Fact]
    public async Task Send_EveryAttemptHangs_GivesUpWithTimeoutWhenTotalTimeoutIsReached()
    {
        // Arrange
        // More retries than fit into the total timeout, so the total timeout ends the request.
        _httpClient.Dispose();
        _httpClient = CreateHttpClient(Settings with { MaxRetryAttempts = 5 });
        _handler.CallsToSendAsync().ReturnsLazily(call => HangAsync(call));

        // Act
        var act = () => SendAsync(TestContext.Current.CancellationToken);

        // Assert
        await act.ShouldThrowAsync<TimeoutRejectedException>();
        _handler.CallsToSendAsync().MustHaveHappenedANumberOfTimesMatching(count => count < 6);
    }

    [Fact]
    public async Task Send_CallerCancelsWhileRequestIsInFlight_ThrowsOperationCanceledExceptionWithoutRetrying()
    {
        // Arrange
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        _handler.CancelsCallerWhileInFlight(cts);

        // Act
        var act = () => SendAsync(cts.Token);

        // Assert
        await act.ShouldThrowAsync<OperationCanceledException>();
        _handler.CallsToSendAsync().MustHaveHappenedOnceExactly();
    }

    private static Task<HttpResponseMessage> Respond(IFakeObjectCall call, HttpStatusCode statusCode)
    {
        return Task.FromResult(new HttpResponseMessage(statusCode) { RequestMessage = call.GetArgument<HttpRequestMessage>(0) });
    }

    private static async Task<HttpResponseMessage> HangAsync(IFakeObjectCall call)
    {
        await Task.Delay(Timeout.Infinite, call.GetArgument<CancellationToken>(1));

        return new HttpResponseMessage(HttpStatusCode.OK);
    }

    private HttpClient CreateHttpClient(WeakAppResilienceSettings settings)
    {
        var builder = new ResiliencePipelineBuilder<HttpResponseMessage> { TimeProvider = _timeProvider };

        WeakAppResiliencePipeline.Configure(builder, settings);

        var resilienceHandler = new ResilienceHandler(builder.Build()) { InnerHandler = _handler };

        return new HttpClient(resilienceHandler) { BaseAddress = new Uri("http://weakapp.test/") };
    }

    // Retry delays and timeouts run on fake time, so keep moving it forward until the request completes.
    private async Task<HttpResponseMessage> SendAsync(CancellationToken ct)
    {
        var sendTask = _httpClient.GetAsync("meters", ct);

        var stopwatch = Stopwatch.StartNew();

        while (!sendTask.IsCompleted && stopwatch.Elapsed < RealTimeLimit)
        {
            _timeProvider.Advance(TimeStep);

            await Task.Delay(10, TestContext.Current.CancellationToken);
        }

        return await sendTask;
    }
}
