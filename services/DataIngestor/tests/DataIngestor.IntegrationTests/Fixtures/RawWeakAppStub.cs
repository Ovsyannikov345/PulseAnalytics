using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace DataIngestor.IntegrationTests.Fixtures;

/// <summary>
/// A raw-socket stand-in for the WeakApp API, for transport failures that WireMock can't produce.
/// It writes scripted HTTP/1.1 responses byte for byte and closes the connection after each one,
/// so a response can break off in the middle of its body the way WeakApp's do.
/// </summary>
public sealed class RawWeakAppStub : IAsyncDisposable
{
    private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
    private readonly CancellationTokenSource _cts = new();
    private readonly ConcurrentBag<Task> _connections = [];
    private readonly Task _acceptLoop;
    private IReadOnlyList<byte[]> _responses = [];
    private int _requestCount;

    public RawWeakAppStub()
    {
        _listener.Start();
        _acceptLoop = AcceptConnectionsAsync();
    }

    public string BaseUrl => $"http://127.0.0.1:{((IPEndPoint)_listener.LocalEndpoint).Port}";

    public int RequestCount => Volatile.Read(ref _requestCount);

    /// <summary>
    /// Builds the truncated response WeakApp sends for a share of its <c>200</c>s (captured on 2026-11-22):
    /// a chunked body whose only chunk is an error object, after which WeakApp closes the connection
    /// without the terminating zero-length chunk, so HTTP clients report that the response ended prematurely.
    /// </summary>
    /// <returns>The raw bytes of the response.</returns>
    public static byte[] TruncatedReadings()
    {
        const string Chunk = """{"error": "data corrupted"}""";

        return Encoding.ASCII.GetBytes(
            "HTTP/1.1 200 OK\r\n" +
            "Content-Type: application/json\r\n" +
            "Transfer-Encoding: chunked\r\n" +
            "\r\n" +
            $"{Chunk.Length:x}\r\n{Chunk}\r\n");
    }

    /// <summary>
    /// Builds a complete <c>200</c> response with the given readings.
    /// </summary>
    /// <param name="json">The response body.</param>
    /// <returns>The raw bytes of the response.</returns>
    public static byte[] Readings(string json = WeakAppStub.AllReadingsJson)
    {
        var body = Encoding.UTF8.GetBytes(json);

        var head = Encoding.ASCII.GetBytes(
            "HTTP/1.1 200 OK\r\n" +
            "Content-Type: application/json\r\n" +
            $"Content-Length: {body.Length}\r\n" +
            "Connection: close\r\n" +
            "\r\n");

        return [.. head, .. body];
    }

    /// <summary>
    /// Serves the responses one per request, in order; the last one is repeated for every further request.
    /// </summary>
    /// <param name="responses">The raw responses to serve.</param>
    public void RespondsInSequence(params byte[][] responses)
    {
        _responses = responses;
    }

    public async ValueTask DisposeAsync()
    {
        await _cts.CancelAsync();
        _listener.Stop();

        await _acceptLoop;
        await Task.WhenAll(_connections);

        _cts.Dispose();
    }

    private async Task AcceptConnectionsAsync()
    {
        while (true)
        {
            TcpClient client;

            try
            {
                client = await _listener.AcceptTcpClientAsync(_cts.Token);
            }
            catch (Exception ex) when (ex is OperationCanceledException or SocketException or ObjectDisposedException)
            {
                return;
            }

            _connections.Add(ServeAsync(client));
        }
    }

    private async Task ServeAsync(TcpClient client)
    {
        using (client)
        {
            try
            {
                var stream = client.GetStream();

                await ReadRequestHeadAsync(stream);

                var index = Interlocked.Increment(ref _requestCount) - 1;
                var responses = _responses;

                await stream.WriteAsync(responses[Math.Min(index, responses.Count - 1)], _cts.Token);
                await stream.FlushAsync(_cts.Token);
            }
            catch (Exception ex) when (ex is IOException or OperationCanceledException or ObjectDisposedException)
            {
                // The client went away or the stub is shutting down; there is nobody left to answer.
            }
        }
    }

    // Reads until the blank line that ends the request head; the ingestor only sends GETs, so there is no body.
    private async Task ReadRequestHeadAsync(NetworkStream stream)
    {
        var buffer = new byte[1];
        var tail = 0u;

        while (tail != 0x0D0A0D0A)
        {
            if (await stream.ReadAsync(buffer, _cts.Token) == 0)
            {
                throw new IOException("The connection closed before the request head was complete.");
            }

            tail = (tail << 8) | buffer[0];
        }
    }
}
