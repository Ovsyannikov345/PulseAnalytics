namespace DataIngestor.Infrastructure.WeakApp.Resilience;

/// <summary>
/// Reads the whole response body before handing the response back up the handler chain.
/// </summary>
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
