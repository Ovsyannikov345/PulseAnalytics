using DataIngestor.Application.Correlation;
using Shouldly;
using Xunit;

namespace DataIngestor.UnitTests.Application.Correlation;

public sealed class CorrelationContextTests
{
    [Fact]
    public void CorrelationId_NotSet_CreatesVersion7Id()
    {
        // Act
        var correlationId = CorrelationContext.CorrelationId;

        // Assert
        correlationId.Version.ShouldBe(7);
    }

    [Fact]
    public void CorrelationId_ReadRepeatedly_ReturnsSameId()
    {
        // Act
        var first = CorrelationContext.CorrelationId;
        var second = CorrelationContext.CorrelationId;

        // Assert
        second.ShouldBe(first);
    }

    [Fact]
    public void CorrelationId_Set_ReturnsAssignedId()
    {
        // Arrange
        var correlationId = Guid.CreateVersion7();

        // Act
        CorrelationContext.CorrelationId = correlationId;

        // Assert
        CorrelationContext.CorrelationId.ShouldBe(correlationId);
    }

    [Fact]
    public void Reset_AfterIdWasRead_NextReadCreatesNewId()
    {
        // Arrange
        var previous = CorrelationContext.CorrelationId;

        // Act
        CorrelationContext.Reset();

        // Assert
        CorrelationContext.CorrelationId.ShouldNotBe(previous);
    }

    [Fact]
    public async Task CorrelationId_SetBeforeAwaitedCall_FlowsIntoIt()
    {
        // Arrange
        var correlationId = Guid.CreateVersion7();
        CorrelationContext.CorrelationId = correlationId;

        // Act
        var observed = await ReadInChildFlowAsync();

        // Assert
        observed.ShouldBe(correlationId);
    }

    [Fact]
    public async Task CorrelationId_SetInAwaitedCall_DoesNotFlowBackToCaller()
    {
        // Arrange
        var callerId = CorrelationContext.CorrelationId;

        // Act
        await SetInChildFlowAsync(Guid.CreateVersion7());

        // Assert
        CorrelationContext.CorrelationId.ShouldBe(callerId);
    }

    private static async Task<Guid> ReadInChildFlowAsync()
    {
        await Task.Yield();

        return CorrelationContext.CorrelationId;
    }

    private static async Task SetInChildFlowAsync(Guid correlationId)
    {
        await Task.Yield();

        CorrelationContext.CorrelationId = correlationId;
    }
}
