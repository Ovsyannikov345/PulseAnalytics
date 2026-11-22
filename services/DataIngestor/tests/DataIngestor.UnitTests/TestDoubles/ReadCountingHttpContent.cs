using System.Net;
using System.Text;

namespace DataIngestor.UnitTests.TestDoubles;

/// <summary>
/// HTTP content that counts how many times its body was read from the source. Once the content is buffered,
/// further reads are served from the buffer, so the count shows whether and when the body was buffered.
/// </summary>
internal sealed class ReadCountingHttpContent(string body) : HttpContent
{
    private readonly byte[] _bytes = Encoding.UTF8.GetBytes(body);

    public int ReadCount { get; private set; }

    protected override async Task SerializeToStreamAsync(Stream stream, TransportContext? context)
    {
        ReadCount++;

        await stream.WriteAsync(_bytes);
    }

    protected override bool TryComputeLength(out long length)
    {
        length = _bytes.Length;

        return true;
    }
}
