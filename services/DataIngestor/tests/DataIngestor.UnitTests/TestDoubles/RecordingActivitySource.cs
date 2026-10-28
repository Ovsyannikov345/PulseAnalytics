using System.Collections.Concurrent;
using System.Diagnostics;

namespace DataIngestor.UnitTests.TestDoubles;

/// <summary>
/// Activity source with a unique name and its own listener, so a test sees only the activities it started,
/// even while other test classes run in parallel.
/// </summary>
internal sealed class RecordingActivitySource : IDisposable
{
    private readonly ActivitySource _source = new($"DataIngestor.UnitTests.{Guid.NewGuid():N}");
    private readonly ActivityListener _listener;
    private readonly ConcurrentQueue<Activity> _stoppedActivities = new();

    public RecordingActivitySource()
    {
        _listener = new ActivityListener
        {
            ShouldListenTo = source => ReferenceEquals(source, _source),
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = _stoppedActivities.Enqueue
        };
        ActivitySource.AddActivityListener(_listener);
    }

    public IReadOnlyCollection<Activity> StoppedActivities => _stoppedActivities;

    public Activity Start(string name = "test activity")
    {
        return _source.StartActivity(name) ?? throw new InvalidOperationException("Activity was not sampled");
    }

    public void Dispose()
    {
        _listener.Dispose();
        _source.Dispose();
    }
}
