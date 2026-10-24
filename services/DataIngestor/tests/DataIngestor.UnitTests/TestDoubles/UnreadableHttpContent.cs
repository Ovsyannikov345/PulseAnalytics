using System.Net;

namespace DataIngestor.UnitTests.TestDoubles;

/// <summary>
/// HTTP content whose body fails while being read, as when the connection drops mid-response.
/// </summary>
internal sealed class UnreadableHttpContent : HttpContent
{
    protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context)
    {
        throw new IOException("The connection was closed while reading the response body.");
    }

    protected override bool TryComputeLength(out long length)
    {
        length = 0;

        return false;
    }
}
