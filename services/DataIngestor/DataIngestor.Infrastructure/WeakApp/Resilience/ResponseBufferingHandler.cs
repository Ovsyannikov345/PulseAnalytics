namespace DataIngestor.Infrastructure.WeakApp.Resilience;

/// <summary>
/// Reads the whole response body before handing the response back up the handler chain.
/// </summary>
/// <remarks>
/// <see cref="HttpClient"/> buffers the body only after the whole handler chain has returned, which is outside
/// the resilience handler. Registered inside the resilience handler, this handler reads the body as part of each
/// attempt, so a body that breaks off mid-read (WeakApp sometimes sends <c>200</c> and then drops the connection)
/// fails that attempt with an <see cref="HttpRequestException"/> and is retried, and the read is bounded by the
/// per-attempt timeout.
/// </remarks>
internal sealed class ResponseBufferingHandler : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var response = await base.SendAsync(request, cancellationToken);

        try
        {
            await response.Content.LoadIntoBufferAsync(cancellationToken);
        }
        catch
        {
            response.Dispose();

            throw;
        }

        return response;
    }
}
