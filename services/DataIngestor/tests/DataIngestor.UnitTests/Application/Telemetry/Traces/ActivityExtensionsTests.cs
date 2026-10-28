using System.Diagnostics;
using DataIngestor.Application.Telemetry.Traces;
using DataIngestor.UnitTests.TestDoubles;
using Shouldly;
using Xunit;

namespace DataIngestor.UnitTests.Application.Telemetry.Traces;

public sealed class ActivityExtensionsTests : IDisposable
{
    private readonly RecordingActivitySource _activitySource = new();

    public void Dispose()
    {
        _activitySource.Dispose();
    }

    [Fact]
    public void MarkFailed_Reason_SetsErrorStatusWithReason()
    {
        // Arrange
        using var activity = _activitySource.Start();

        // Act
        activity.MarkFailed("Weak App API responded with failure");

        // Assert
        activity.Status.ShouldBe(ActivityStatusCode.Error);
        activity.StatusDescription.ShouldBe("Weak App API responded with failure");
        activity.Events.ShouldBeEmpty();
    }

    [Fact]
    public void MarkFailed_Exception_RecordsExceptionAndSetsErrorStatusAndType()
    {
        // Arrange
        using var activity = _activitySource.Start();
        var exception = new InvalidOperationException("Unexpected failure");

        // Act
        activity.MarkFailed(exception);

        // Assert
        activity.Status.ShouldBe(ActivityStatusCode.Error);
        activity.StatusDescription.ShouldBe("Unexpected failure");
        activity.GetTagItem("error.type").ShouldBe(typeof(InvalidOperationException).FullName);
        var exceptionEvent = activity.Events.ShouldHaveSingleItem();
        exceptionEvent.Name.ShouldBe("exception");
        exceptionEvent.Tags.ShouldContain(tag => tag.Key == "exception.message" && Equals(tag.Value, "Unexpected failure"));
    }

    [Fact]
    public void MarkFailed_NoActivity_DoesNothing()
    {
        // Arrange
        Activity? activity = null;

        // Act
        var markWithReason = () => activity.MarkFailed("Weak App API responded with failure");
        var markWithException = () => activity.MarkFailed(new InvalidOperationException("Unexpected failure"));

        // Assert
        markWithReason.ShouldNotThrow();
        markWithException.ShouldNotThrow();
    }
}
