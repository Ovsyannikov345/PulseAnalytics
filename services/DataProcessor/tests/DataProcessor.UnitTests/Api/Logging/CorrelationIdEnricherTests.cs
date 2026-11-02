using DataProcessor.Api.Logging;
using DataProcessor.Application.Correlation;
using DataProcessor.UnitTests.TestDoubles;
using Serilog;
using Serilog.Events;
using Shouldly;
using Xunit;

namespace DataProcessor.UnitTests.Api.Logging;

public sealed class CorrelationIdEnricherTests
{
    private readonly CollectingLogEventSink _sink = new();
    private readonly ILogger _logger;

    public CorrelationIdEnricherTests()
    {
        _logger = new LoggerConfiguration()
            .Enrich.With<CorrelationIdEnricher>()
            .WriteTo.Sink(_sink)
            .CreateLogger();
    }

    [Fact]
    public void Enrich_CorrelationIdSet_AddsItToEvent()
    {
        // Arrange
        var correlationId = Guid.CreateVersion7();
        CorrelationContext.CorrelationId = correlationId;

        // Act
        _logger.Information("Stored {Type} reading", "energy");

        // Assert
        GetCorrelationId(_sink.Events.ShouldHaveSingleItem()).ShouldBe(correlationId);
    }

    [Fact]
    public void Enrich_CorrelationIdNotSet_AddsIdCreatedForCurrentFlow()
    {
        // Act
        _logger.Information("RabbitMQ consumer is shutting down");

        // Assert
        var correlationId = GetCorrelationId(_sink.Events.ShouldHaveSingleItem());
        correlationId.Version.ShouldBe(7);
        correlationId.ShouldBe(CorrelationContext.CorrelationId);
    }

    private static Guid GetCorrelationId(LogEvent logEvent)
    {
        var property = logEvent.Properties[CorrelationIdEnricher.PropertyName].ShouldBeOfType<ScalarValue>();

        return property.Value.ShouldBeOfType<Guid>();
    }
}
