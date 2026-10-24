using System.Collections.Concurrent;
using FakeItEasy;
using FakeItEasy.Core;
using Microsoft.Extensions.Logging;

namespace DataIngestor.UnitTests.TestDoubles;

/// <summary>
/// Logger provider whose loggers are FakeItEasy fakes that record every entry written through them,
/// so tests can assert on what the code under test logged.
/// </summary>
internal sealed class CapturingLoggerProvider
{
    private readonly ConcurrentQueue<LogEntry> _entries = new();

    public CapturingLoggerProvider()
    {
        Provider = A.Fake<ILoggerProvider>();

        A.CallTo(() => Provider.CreateLogger(A<string>._))
            .ReturnsLazily((string categoryName) => CreateLogger(categoryName));
    }

    public ILoggerProvider Provider { get; }

    public IReadOnlyList<LogEntry> Entries => [.. _entries];

    private static LogEntry ToLogEntry(string categoryName, IFakeObjectCall call)
    {
        // ILogger.Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        var level = call.GetArgument<LogLevel>(0);
        var state = call.Arguments[2];
        var exception = call.GetArgument<Exception?>(3);
        var formatter = call.GetArgument<Delegate>(4)!;

        var message = (string?)formatter.DynamicInvoke(state, exception) ?? string.Empty;

        return new LogEntry(categoryName, level, message, exception);
    }

    private ILogger CreateLogger(string categoryName)
    {
        var logger = A.Fake<ILogger>();

        A.CallTo(() => logger.IsEnabled(A<LogLevel>._)).Returns(true);

        A.CallTo(logger)
            .Where(call => call.Method.Name == nameof(ILogger.Log))
            .Invokes(call => _entries.Enqueue(ToLogEntry(categoryName, call)));

        return logger;
    }
}

internal sealed record LogEntry(string Category, LogLevel Level, string Message, Exception? Exception);
