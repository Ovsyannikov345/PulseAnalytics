using System.Net;

namespace DataIngestor.UnitTests.TestDoubles;

/// <summary>
/// HTTP content whose body fails while being read, as when the connection drops mid-response.
/// Records whether it was disposed, which shows whether the code under test released the response.
/// </summary>
internal sealed class UnreadableHttpContent : HttpContent
{
    public bool IsDisposed { get; private set; }

    protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context)
    {
        throw new IOException("The connection was closed while reading the response body.");
    }

    protected override bool TryComputeLength(out long length)
    {
        length = 0;

        return false;
    }

    protected override void Dispose(bool disposing)
    {
        IsDisposed = true;

        base.Dispose(disposing);
    }
}
