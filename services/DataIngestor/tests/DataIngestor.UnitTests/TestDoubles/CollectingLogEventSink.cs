using System.Collections.Concurrent;
using Serilog.Core;
using Serilog.Events;

namespace DataIngestor.UnitTests.TestDoubles;

/// <summary>
/// Serilog sink that keeps every emitted event in memory so tests can inspect its properties.
/// </summary>
internal sealed class CollectingLogEventSink : ILogEventSink
{
    private readonly ConcurrentQueue<LogEvent> _events = new();

    public IReadOnlyCollection<LogEvent> Events => _events;

    public void Emit(LogEvent logEvent)
    {
        _events.Enqueue(logEvent);
    }
}
