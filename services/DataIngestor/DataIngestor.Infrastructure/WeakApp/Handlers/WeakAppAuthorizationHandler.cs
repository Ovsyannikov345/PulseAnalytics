using DataIngestor.Infrastructure.WeakApp.Configuration;
using DataIngestor.Infrastructure.WeakApp.Constants;
using Microsoft.Extensions.Options;

namespace DataIngestor.Infrastructure.WeakApp.Handlers;

internal sealed class WeakAppAuthorizationHandler(IOptionsMonitor<WeakAppSettings> options) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var settings = options.CurrentValue;

        request.Headers.Add(HeaderNames.ApiKey, settings.ApiKey);

        return await base.SendAsync(request, cancellationToken);
    }
}
