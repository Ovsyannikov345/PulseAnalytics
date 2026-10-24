using System.Collections.Concurrent;
using FakeItEasy;
using FakeItEasy.Core;
using Microsoft.Extensions.Logging;

namespace DataIngestor.UnitTests.TestDoubles;

/// <summary>
/// Creates FakeItEasy logger fakes that record every entry written through them,
/// so tests can assert on what the code under test logged.
/// </summary>
internal sealed class LogCapture
{
    private readonly ConcurrentQueue<LogEntry> _entries = new();

    public IReadOnlyList<LogEntry> Entries => [.. _entries];

    public ILogger<TCategory> CreateLogger<TCategory>()
    {
        var logger = A.Fake<ILogger<TCategory>>();

        A.CallTo(() => logger.IsEnabled(A<LogLevel>._)).Returns(true);

        A.CallTo(logger)
            .Where(call => call.Method.Name == nameof(ILogger.Log))
            .Invokes(call => _entries.Enqueue(ToLogEntry(call)));

        return logger;
    }

    private static LogEntry ToLogEntry(IFakeObjectCall call)
    {
        // ILogger.Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        var level = call.GetArgument<LogLevel>(0);
        var state = call.Arguments[2];
        var exception = call.GetArgument<Exception?>(3);
        var formatter = call.GetArgument<Delegate>(4)!;

        var message = (string?)formatter.DynamicInvoke(state, exception) ?? string.Empty;

        return new LogEntry(level, message, exception);
    }
}

internal sealed record LogEntry(LogLevel Level, string Message, Exception? Exception);
