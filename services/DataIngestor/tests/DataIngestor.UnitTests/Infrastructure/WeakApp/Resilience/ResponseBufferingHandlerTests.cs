using System.Net;
using DataIngestor.Infrastructure.WeakApp.Resilience;
using DataIngestor.UnitTests.TestDoubles;
using FakeItEasy;
using Shouldly;
using Xunit;

namespace DataIngestor.UnitTests.Infrastructure.WeakApp.Resilience;

public sealed class ResponseBufferingHandlerTests : IDisposable
{
    private readonly HttpMessageHandler _innerHandler = A.Fake<HttpMessageHandler>();
    private readonly HttpMessageInvoker _invoker;

    public ResponseBufferingHandlerTests()
    {
        _invoker = new HttpMessageInvoker(new ResponseBufferingHandler { InnerHandler = _innerHandler });
    }

    public void Dispose()
    {
        _invoker.Dispose();
    }

    [Fact]
    public async Task SendAsync_ReadableBody_ReadsBodyBeforeReturningResponse()
    {
        // Arrange
        var content = new ReadCountingHttpContent("[1,2,3]");
        _innerHandler.RespondsWith(HttpStatusCode.OK, () => content);

        // Act
        using var response = await SendAsync();

        // Assert
        content.ReadCount.ShouldBe(1);
    }

    [Fact]
    public async Task SendAsync_ReadableBody_ServesLaterReadsFromBuffer()
    {
        // Arrange
        var content = new ReadCountingHttpContent("[1,2,3]");
        _innerHandler.RespondsWith(HttpStatusCode.OK, () => content);

        // Act
        using var response = await SendAsync();
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        // Assert
        body.ShouldBe("[1,2,3]");
        content.ReadCount.ShouldBe(1);
    }

    [Fact]
    public async Task SendAsync_BodyFailsMidRead_ThrowsHttpRequestExceptionAndDisposesResponse()
    {
        // Arrange
        var content = new UnreadableHttpContent();
        _innerHandler.RespondsWith(HttpStatusCode.OK, () => content);

        // Act
        var act = SendAsync;

        // Assert
        var exception = await act.ShouldThrowAsync<HttpRequestException>();
        exception.InnerException.ShouldBeOfType<IOException>();
        content.IsDisposed.ShouldBeTrue();
    }

    [Fact]
    public async Task SendAsync_FailureStatusWithReadableBody_ReturnsResponseUnchanged()
    {
        // Arrange
        _innerHandler.RespondsWith(HttpStatusCode.ServiceUnavailable, "Service Unavailable");

        // Act
        using var response = await SendAsync();

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
        (await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).ShouldBe("Service Unavailable");
    }

    [Fact]
    public async Task SendAsync_CallerCancelsWhileBodyIsBeingRead_ThrowsOperationCanceledException()
    {
        // Arrange
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        _innerHandler.RespondsWith(HttpStatusCode.OK, () => new HangingHttpContent());

        // Act
        var sendTask = _invoker.SendAsync(new HttpRequestMessage(HttpMethod.Get, "http://weakapp.test/meters"), cts.Token);
        await cts.CancelAsync();

        // Assert
        await Should.ThrowAsync<OperationCanceledException>(sendTask);
    }

    private Task<HttpResponseMessage> SendAsync()
    {
        return _invoker.SendAsync(
            new HttpRequestMessage(HttpMethod.Get, "http://weakapp.test/meters"),
            TestContext.Current.CancellationToken);
    }
}
