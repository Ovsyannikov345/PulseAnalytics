using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using NotificationService.IntegrationTests.Fixtures;

namespace NotificationService.IntegrationTests.Utilities;

/// <summary>
/// Reads the notification service's per-instance queues through the RabbitMQ management API.
/// </summary>
internal sealed class NotificationQueues(RabbitMqContainerFixture rabbitMq) : IDisposable
{
    public const int QueuesPerInstance = 3;

    private const string QueueNamePrefix = "pulse.meter-readings.stored.";

    private const string QueueNameInfix = ".notify.";

    private static readonly TimeSpan ReadyTimeout = TimeSpan.FromSeconds(30);

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    private readonly HttpClient _client = rabbitMq.CreateManagementClient();

    /// <summary>
    /// Waits until one service instance has all its queues bound to the stored readings exchange. Events published
    /// from then on are routed to them, and wait there until the consumers take them.
    /// </summary>
    public async Task WaitUntilBoundAsync(CancellationToken ct)
    {
        await TestWait.UntilAsync(
            async () =>
            {
                var queues = await GetAsync(ct);
                var boundQueues = await GetBoundQueueNamesAsync(ct);

                return queues.Count == QueuesPerInstance && queues.All(queue => boundQueues.Contains(queue.Name));
            },
            ReadyTimeout,
            ct);
    }

    public async Task WaitUntilDeletedAsync(CancellationToken ct)
    {
        await TestWait.UntilAsync(async () => (await GetAsync(ct)).Count == 0, ReadyTimeout, ct);
    }

    public async Task<IReadOnlyList<QueueInfo>> GetAsync(CancellationToken ct)
    {
        var queues = await _client.GetFromJsonAsync<QueueInfo[]>("queues/%2F", JsonOptions, ct) ?? [];

        return
        [
            .. queues.Where(queue => queue.Name.StartsWith(QueueNamePrefix, StringComparison.Ordinal)
                && queue.Name.Contains(QueueNameInfix, StringComparison.Ordinal)),
        ];
    }

    public void Dispose()
    {
        _client.Dispose();
    }

    private async Task<HashSet<string>> GetBoundQueueNamesAsync(CancellationToken ct)
    {
        using var response = await _client.GetAsync(
            new Uri($"exchanges/%2F/{StoredReadingsExchange.Name}/bindings/source", UriKind.Relative),
            ct);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return [];
        }

        response.EnsureSuccessStatusCode();

        var bindings = await response.Content.ReadFromJsonAsync<BindingInfo[]>(JsonOptions, ct) ?? [];

        return [.. bindings.Select(binding => binding.Destination)];
    }

    private sealed record BindingInfo(string Destination);
}

internal sealed record QueueInfo(
    string Name,
    bool Durable,
    bool Exclusive,
    bool AutoDelete,
    Dictionary<string, JsonElement> Arguments);
