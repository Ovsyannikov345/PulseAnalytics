using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.AspNetCore.TestHost;

namespace NotificationService.IntegrationTests.Utilities;

/// <summary>
/// SignalR client of the readings hub, connected to the test server over WebSockets.
/// Keeps every pushed reading as raw JSON, the way a browser receives it.
/// </summary>
internal sealed class ReadingsHubClient : IAsyncDisposable
{
    private const string HubPath = "hubs/readings";

    private const string ReadingStoredMethod = "ReadingStored";

    private static readonly TimeSpan PushTimeout = TimeSpan.FromSeconds(30);

    private readonly HubConnection _connection;
    private readonly ConcurrentQueue<JsonElement> _readings = new();

    private ReadingsHubClient(TestServer server)
    {
        _connection = new HubConnectionBuilder()
            .WithUrl(new Uri(server.BaseAddress, HubPath), options =>
            {
                options.Transports = HttpTransportType.WebSockets;
                options.HttpMessageHandlerFactory = _ => server.CreateHandler();
                options.WebSocketFactory = (context, ct) => CreateWebSocketAsync(server, context.Uri, ct);
            })
            .Build();

        _connection.On<JsonElement>(ReadingStoredMethod, _readings.Enqueue);
    }

    public IReadOnlyCollection<JsonElement> Readings => _readings;

    public static async Task<ReadingsHubClient> ConnectAsync(TestServer server, CancellationToken ct)
    {
        var client = new ReadingsHubClient(server);

        await client._connection.StartAsync(ct);

        return client;
    }

    /// <summary>
    /// Waits until at least the given number of readings has been pushed, and returns all pushed so far.
    /// </summary>
    public async Task<IReadOnlyList<JsonElement>> WaitForReadingsAsync(int count, CancellationToken ct)
    {
        await TestWait.UntilAsync(() => _readings.Count >= count, PushTimeout, ct);

        return [.. _readings];
    }

    public async ValueTask DisposeAsync()
    {
        await _connection.DisposeAsync();
    }

    private static async ValueTask<System.Net.WebSockets.WebSocket> CreateWebSocketAsync(TestServer server, Uri uri, CancellationToken ct)
    {
        var webSocketClient = server.CreateWebSocketClient();

        return await webSocketClient.ConnectAsync(uri, ct);
    }
}
