using System.Collections.Concurrent;
using System.Diagnostics;
using DataIngestor.Application.Telemetry;
using DataIngestor.Application.Telemetry.Traces;
using Shouldly;
using Xunit;

namespace DataIngestor.UnitTests.Application.Telemetry.Traces;

// The listener is process-wide, so tests only look at activities tagged with their own correlation ID.
public sealed class TracingServiceTests : IDisposable
{
    private readonly ConcurrentQueue<Activity> _startedActivities = new();
    private readonly ActivityListener _listener;
    private readonly TracingService _sut = new();

    public TracingServiceTests()
    {
        _listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == IngestorTelemetry.ScopeName,
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
    public void StartPoll_AnyCorrelationId_StartsInternalPollSpanTaggedWithCorrelationId()
    {
        // Arrange
        var correlationId = Guid.NewGuid();

        // Act
        using var activity = _sut.StartPoll(correlationId);

        // Assert
        activity.ShouldNotBeNull();
        activity.Source.Name.ShouldBe(IngestorTelemetry.ScopeName);
        activity.OperationName.ShouldBe(IngestorTelemetry.SpanNames.Polling);
        activity.Kind.ShouldBe(ActivityKind.Internal);
        activity.GetTagItem("app.correlation_id").ShouldBe(correlationId.ToString());
        StartedActivitiesFor(correlationId).ShouldHaveSingleItem().ShouldBeSameAs(activity);
    }

    [Fact]
    public void StartPoll_ParentActivityIsCurrent_StartsPollSpanAsItsChild()
    {
        // Arrange
        var correlationId = Guid.NewGuid();
        using var parentSource = new ActivitySource($"DataIngestor.UnitTests.{Guid.NewGuid():N}");
        using var parentListener = new ActivityListener
        {
            ShouldListenTo = source => ReferenceEquals(source, parentSource),
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded
        };
        ActivitySource.AddActivityListener(parentListener);
        using var parent = parentSource.StartActivity("parent");

        // Act
        using var activity = _sut.StartPoll(correlationId);

        // Assert
        activity.ShouldNotBeNull().ParentSpanId.ShouldBe(parent.ShouldNotBeNull().SpanId);
    }

    private IEnumerable<Activity> StartedActivitiesFor(Guid correlationId)
    {
        return _startedActivities.Where(activity => Equals(activity.GetTagItem("app.correlation_id"), correlationId.ToString()));
    }
}
