using Microsoft.Extensions.Time.Testing;

namespace DataIngestor.UnitTests.TestDoubles;

/// <summary>
/// <see cref="FakeTimeProvider"/> that counts the timers created through it. Code running in the background
/// (e.g. Task.Delay or PeriodicTimer) only reacts to <see cref="FakeTimeProvider.Advance"/> once its timer exists,
/// so tests wait for the timer before advancing time.
/// </summary>
internal sealed class TimerTrackingFakeTimeProvider : FakeTimeProvider
{
    private int _createdTimersCount;

    public int CreatedTimersCount => Volatile.Read(ref _createdTimersCount);

    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        var timer = base.CreateTimer(callback, state, dueTime, period);

        Interlocked.Increment(ref _createdTimersCount);

        return timer;
    }
}
