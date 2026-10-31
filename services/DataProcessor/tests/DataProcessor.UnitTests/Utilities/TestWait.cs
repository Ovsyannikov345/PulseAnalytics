using System.Diagnostics;

namespace DataProcessor.UnitTests.Utilities;

/// <summary>
/// Waits for a condition produced by background work (e.g. a hosted service) to become true.
/// </summary>
internal static class TestWait
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(10);

    public static async Task UntilAsync(Func<bool> condition, CancellationToken ct)
    {
        var stopwatch = Stopwatch.StartNew();

        while (!condition())
        {
            if (stopwatch.Elapsed > Timeout)
            {
                throw new TimeoutException($"The condition was not met within {Timeout.TotalSeconds} seconds.");
            }

            await Task.Delay(PollInterval, ct);
        }
    }
}
