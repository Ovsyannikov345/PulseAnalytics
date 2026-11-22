using System.Net;

namespace DataIngestor.UnitTests.TestDoubles;

/// <summary>
/// HTTP content whose body never finishes arriving: reading it waits until the read is cancelled,
/// as when the server sends the headers and then stalls.
/// </summary>
internal sealed class HangingHttpContent : HttpContent
{
    protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context)
    {
        return SerializeToStreamAsync(stream, context, CancellationToken.None);
    }

    protected override async Task SerializeToStreamAsync(Stream stream, TransportContext? context, CancellationToken cancellationToken)
    {
        await Task.Delay(Timeout.Infinite, cancellationToken);
    }

    protected override bool TryComputeLength(out long length)
    {
        length = 0;

        return false;
    }
}
