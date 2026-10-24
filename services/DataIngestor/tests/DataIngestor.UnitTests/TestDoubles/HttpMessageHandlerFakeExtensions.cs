using System.Net;
using System.Text;
using FakeItEasy;
using FakeItEasy.Configuration;

namespace DataIngestor.UnitTests.TestDoubles;

/// <summary>
/// Helpers for configuring a FakeItEasy fake of <see cref="HttpMessageHandler"/>, whose <c>SendAsync</c> is protected.
/// </summary>
internal static class HttpMessageHandlerFakeExtensions
{
    private const string SendAsyncMethodName = "SendAsync";

    public static IAnyCallConfigurationWithReturnTypeSpecified<Task<HttpResponseMessage>> CallsToSendAsync(
        this HttpMessageHandler handler)
    {
        return A.CallTo(handler)
            .Where(call => call.Method.Name == SendAsyncMethodName)
            .WithReturnType<Task<HttpResponseMessage>>();
    }

    public static void RespondsWithJson(this HttpMessageHandler handler, string json)
    {
        handler.RespondsWith(HttpStatusCode.OK, () => new StringContent(json, Encoding.UTF8, "application/json"));
    }

    public static void RespondsWith(this HttpMessageHandler handler, HttpStatusCode statusCode, string body)
    {
        handler.RespondsWith(statusCode, () => new StringContent(body, Encoding.UTF8, "text/plain"));
    }

    public static void RespondsWith(this HttpMessageHandler handler, HttpStatusCode statusCode, Func<HttpContent> createContent)
    {
        handler.CallsToSendAsync()
            .ReturnsLazily(call => Task.FromResult(new HttpResponseMessage(statusCode)
            {
                Content = createContent(),
                RequestMessage = call.GetArgument<HttpRequestMessage>(0)
            }));
    }

    /// <summary>
    /// Simulates the caller cancelling while the request is in flight: the handler cancels the caller's
    /// token itself and then waits until that cancellation reaches the request.
    /// </summary>
    public static void CancelsCallerWhileInFlight(this HttpMessageHandler handler, CancellationTokenSource callerCts)
    {
        handler.CallsToSendAsync()
            .ReturnsLazily(async call =>
            {
                await callerCts.CancelAsync();

                await Task.Delay(Timeout.Infinite, call.GetArgument<CancellationToken>(1));

                return new HttpResponseMessage(HttpStatusCode.OK);
            });
    }

    public static HttpRequestMessage SingleReceivedRequest(this HttpMessageHandler handler)
    {
        return Fake.GetCalls(handler)
            .Single(call => call.Method.Name == SendAsyncMethodName)
            .GetArgument<HttpRequestMessage>(0)!;
    }
}
