using System.Diagnostics;

namespace DataIngestor.IntegrationTests.Utilities;

/// <summary>
/// Waits for a condition produced by background work (the polling worker, broker deliveries) to become true.
/// </summary>
internal static class TestWait
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(50);

    public static async Task UntilAsync(Func<bool> condition, TimeSpan timeout, CancellationToken ct)
    {
        var stopwatch = Stopwatch.StartNew();

        while (!condition())
        {
            if (stopwatch.Elapsed > timeout)
            {
                throw new TimeoutException($"The condition was not met within {timeout.TotalSeconds} seconds.");
            }

            await Task.Delay(PollInterval, ct);
        }
    }
}
