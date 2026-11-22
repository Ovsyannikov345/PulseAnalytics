using FakeItEasy;
using FakeItEasy.Configuration;
using RabbitMQ.Client;

namespace DataProcessor.UnitTests.TestDoubles;

/// <summary>
/// Helpers for configuring and inspecting a FakeItEasy fake of <see cref="IChannel"/>.
/// </summary>
internal static class RabbitMqChannelFakeExtensions
{
    private const string BasicConsumeAsyncMethodName = nameof(IChannel.BasicConsumeAsync);

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
                    call.GetArgument<ReadOnlyMemory<byte>>(4).ToArray())),
        ];
    }

    // The BasicConsumeAsync extension used by the code under test calls this full overload.
    public static IReturnValueArgumentValidationConfiguration<Task<string>> CallsToBasicConsumeAsync(this IChannel channel)
    {
        return A.CallTo(() => channel.BasicConsumeAsync(
            A<string>._,
            A<bool>._,
            A<string>._,
            A<bool>._,
            A<bool>._,
            A<IDictionary<string, object?>?>._,
            A<IAsyncBasicConsumer>._,
            A<CancellationToken>._));
    }

    public static IReturnValueArgumentValidationConfiguration<Task<QueueDeclareOk>> CallsToQueueDeclareAsync(this IChannel channel)
    {
        return A.CallTo(() => channel.QueueDeclareAsync(
            A<string>._,
            A<bool>._,
            A<bool>._,
            A<bool>._,
            A<IDictionary<string, object?>?>._,
            A<bool>._,
            A<bool>._,
            A<CancellationToken>._));
    }

    // The CloseAsync extension used by the code under test calls this full overload.
    public static IReturnValueArgumentValidationConfiguration<Task> CallsToCloseAsync(this IChannel channel)
    {
        return A.CallTo(() => channel.CloseAsync(A<ushort>._, A<string>._, A<bool>._, A<CancellationToken>._));
    }

    public static IReturnValueArgumentValidationConfiguration<ValueTask> CallsToBasicAckAsync(this IChannel channel)
    {
        return A.CallTo(() => channel.BasicAckAsync(A<ulong>._, A<bool>._, A<CancellationToken>._));
    }

    public static IReturnValueArgumentValidationConfiguration<ValueTask> CallsToBasicNackAsync(this IChannel channel)
    {
        return A.CallTo(() => channel.BasicNackAsync(A<ulong>._, A<bool>._, A<bool>._, A<CancellationToken>._));
    }

    /// <summary>
    /// Returns the consumers the code under test registered on the channel, so tests can deliver messages to them.
    /// </summary>
    public static IReadOnlyList<IAsyncBasicConsumer> RegisteredConsumers(this IChannel channel)
    {
        return
        [
            .. Fake.GetCalls(channel)
                .Where(call => call.Method.Name == BasicConsumeAsyncMethodName)
                .Select(call => call.GetArgument<IAsyncBasicConsumer>(6)!),
        ];
    }
}

internal sealed record PublishedMessage(string Exchange, string RoutingKey, bool Mandatory, BasicProperties Properties, byte[] Body);
