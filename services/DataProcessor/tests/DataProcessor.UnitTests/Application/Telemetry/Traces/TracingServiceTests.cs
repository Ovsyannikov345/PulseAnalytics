using System.Collections.Concurrent;
using System.Diagnostics;
using DataProcessor.Application.Telemetry;
using DataProcessor.Application.Telemetry.Traces;
using Shouldly;
using Xunit;

namespace DataProcessor.UnitTests.Application.Telemetry.Traces;

// The listener is process-wide, so tests only look at activities tagged with their own correlation ID.
public sealed class TracingServiceTests : IDisposable
{
    private const string MessageType = "MotionReadingReceived";
    private const string MessageId = "0199f5c4-3a1e-7b52-9d0e-5c8a7e6b4f21";

    private readonly ConcurrentQueue<Activity> _startedActivities = new();
    private readonly ActivityListener _listener;
    private readonly TracingService _sut = new();

    public TracingServiceTests()
    {
        _listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == ProcessorTelemetry.ScopeName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStarted = _startedActivities.Enqueue
        };
        ActivitySource.AddActivityListener(_listener);
    }

    public void Dispose()
    {
        _sut.Dispose();
        _listener.Dispose();
    }

    [Fact]
    public void StartMessageProcessing_AnyMessage_StartsInternalProcessingSpanTaggedWithMessageAndCorrelationId()
    {
        // Arrange
        var correlationId = Guid.NewGuid();

        // Act
        using var activity = _sut.StartMessageProcessing(MessageType, MessageId, correlationId);

        // Assert
        activity.ShouldNotBeNull();
        activity.Source.Name.ShouldBe(ProcessorTelemetry.ScopeName);
        activity.OperationName.ShouldBe(ProcessorTelemetry.SpanNames.Processing);
        activity.Kind.ShouldBe(ActivityKind.Internal);
        activity.GetTagItem("app.message.type").ShouldBe(MessageType);
        activity.GetTagItem("messaging.message.id").ShouldBe(MessageId);
        activity.GetTagItem("app.correlation_id").ShouldBe(correlationId.ToString());
        StartedActivitiesFor(correlationId).ShouldHaveSingleItem().ShouldBeSameAs(activity);
    }

    [Fact]
    public void StartMessageProcessing_NoMessageId_StartsSpanWithoutMessageIdTag()
    {
        // Act
        using var activity = _sut.StartMessageProcessing(MessageType, messageId: null, Guid.NewGuid());

        // Assert
        activity.ShouldNotBeNull().GetTagItem("messaging.message.id").ShouldBeNull();
    }

    [Fact]
    public void StartMessageProcessing_ParentActivityIsCurrent_StartsProcessingSpanAsItsChild()
    {
        // Arrange
        var correlationId = Guid.NewGuid();
        using var parentSource = new ActivitySource($"DataProcessor.UnitTests.{Guid.NewGuid():N}");
        using var parentListener = new ActivityListener
        {
            ShouldListenTo = source => ReferenceEquals(source, parentSource),
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded
        };
        ActivitySource.AddActivityListener(parentListener);
        using var parent = parentSource.StartActivity("deliver");

        // Act
        using var activity = _sut.StartMessageProcessing(MessageType, MessageId, correlationId);

        // Assert
        var parentActivity = parent.ShouldNotBeNull();
        activity.ShouldNotBeNull().ParentSpanId.ShouldBe(parentActivity.SpanId);
        activity.TraceId.ShouldBe(parentActivity.TraceId);
    }

    private IEnumerable<Activity> StartedActivitiesFor(Guid correlationId)
    {
        return _startedActivities.Where(activity => Equals(activity.GetTagItem("app.correlation_id"), correlationId.ToString()));
    }
}
