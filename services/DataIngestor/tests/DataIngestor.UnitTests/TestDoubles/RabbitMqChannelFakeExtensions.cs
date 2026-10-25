using FakeItEasy;
using FakeItEasy.Configuration;
using RabbitMQ.Client;

namespace DataIngestor.UnitTests.TestDoubles;

/// <summary>
/// Helpers for configuring and inspecting a FakeItEasy fake of <see cref="IChannel"/>.
/// </summary>
internal static class RabbitMqChannelFakeExtensions
{
    private const string BasicPublishAsyncMethodName = nameof(IChannel.BasicPublishAsync);

    public static IReturnValueArgumentValidationConfiguration<ValueTask> CallsToBasicPublishAsync(this IChannel channel)
    {
        return A.CallTo(() => channel.BasicPublishAsync(
            A<string>._,
            A<string>._,
            A<bool>._,
            A<BasicProperties>._,
            A<ReadOnlyMemory<byte>>._,
            A<CancellationToken>._));
    }

    // The ExchangeDeclareAsync extension used by the code under test calls this full overload.
    public static IReturnValueArgumentValidationConfiguration<Task> CallsToExchangeDeclareAsync(this IChannel channel)
    {
        return A.CallTo(() => channel.ExchangeDeclareAsync(
            A<string>._,
            A<string>._,
            A<bool>._,
            A<bool>._,
            A<IDictionary<string, object?>?>._,
            A<bool>._,
            A<bool>._,
            A<CancellationToken>._));
    }

    /// <summary>
    /// Simulates a publish whose broker confirmation never arrives: it completes only when its token is cancelled.
    /// </summary>
    public static void HangsUntilPublishIsCancelled(this IChannel channel)
    {
        channel.CallsToBasicPublishAsync()
            .ReturnsLazily(call => new ValueTask(Task.Delay(Timeout.Infinite, call.GetArgument<CancellationToken>(5))));
    }

    /// <summary>
    /// Simulates the caller cancelling while a publish is in flight: the channel cancels the caller's
    /// token itself and then waits until that cancellation reaches the publish.
    /// </summary>
    public static void CancelsCallerWhilePublishing(this IChannel channel, CancellationTokenSource callerCts)
    {
        channel.CallsToBasicPublishAsync()
            .ReturnsLazily(call => new ValueTask(CancelAndWaitAsync(call.GetArgument<CancellationToken>(5))));

        async Task CancelAndWaitAsync(CancellationToken publishToken)
        {
            await callerCts.CancelAsync();

            await Task.Delay(Timeout.Infinite, publishToken);
        }
    }

    public static IReadOnlyList<PublishedMessage> PublishedMessages(this IChannel channel)
    {
        return
        [
            .. Fake.GetCalls(channel)
                .Where(call => call.Method.Name == BasicPublishAsyncMethodName)
                .Select(call => new PublishedMessage(
                    call.GetArgument<string>(0)!,
                    call.GetArgument<string>(1)!,
                    call.GetArgument<bool>(2),
                    call.GetArgument<BasicProperties>(3)!,
                    call.GetArgument<ReadOnlyMemory<byte>>(4).ToArray()))
        ];
    }
}

internal sealed record PublishedMessage(string Exchange, string RoutingKey, bool Mandatory, BasicProperties Properties, byte[] Body);
