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
                .Select(call => call.GetArgument<IAsyncBasicConsumer>(6)!)
        ];
    }
}
