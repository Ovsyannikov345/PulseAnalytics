using System.Diagnostics;

namespace DataProcessor.IntegrationTests.Utilities;

/// <summary>
/// Waits for a condition produced by background work (the consumers, broker deliveries) to become true.
/// </summary>
internal static class TestWait
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(50);

    public static Task UntilAsync(Func<bool> condition, TimeSpan timeout, CancellationToken ct)
    {
        return UntilAsync(() => Task.FromResult(condition()), timeout, ct);
    }

    public static async Task UntilAsync(Func<Task<bool>> condition, TimeSpan timeout, CancellationToken ct)
    {
        var stopwatch = Stopwatch.StartNew();

        while (!await condition())
        {
            if (stopwatch.Elapsed > timeout)
            {
                throw new TimeoutException($"The condition was not met within {timeout.TotalSeconds} seconds.");
            }

            await Task.Delay(PollInterval, ct);
        }
    }
}
