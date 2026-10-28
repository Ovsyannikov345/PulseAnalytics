using System.Diagnostics.Metrics;
using DataIngestor.Application.Telemetry;
using DataIngestor.Application.Telemetry.Metrics;
using DataIngestor.Application.Telemetry.Metrics.Enums;
using DataIngestor.UnitTests.Utilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.Metrics.Testing;
using Shouldly;
using Xunit;

namespace DataIngestor.UnitTests.Application.Telemetry;

// Each test class instance gets its own meter factory, so collectors only see what the current test records.
public sealed class MetricsServiceTests : IDisposable
{
    private readonly ServiceProvider _serviceProvider;
    private readonly IMeterFactory _meterFactory;
    private readonly MetricsService _sut;

    public MetricsServiceTests()
    {
        _serviceProvider = new ServiceCollection().AddMetrics().BuildServiceProvider();
        _meterFactory = _serviceProvider.GetRequiredService<IMeterFactory>();

        _sut = new MetricsService(_meterFactory);
    }

    public void Dispose()
    {
        _serviceProvider.Dispose();
    }

    [Theory]
    [InlineData(PollOutcome.Succeeded, "succeeded")]
    [InlineData(PollOutcome.Failed, "failed")]
    public void RecordPoll_AnyOutcome_RecordsDurationInSecondsWithOutcome(PollOutcome outcome, string expectedOutcome)
    {
        // Arrange
        using var collector = Collect<double>(MetricsService.PollDurationName);

        // Act
        _sut.RecordPoll(TimeSpan.FromMilliseconds(1500), outcome);

        // Assert
        collector.Instrument.ShouldNotBeNull().Unit.ShouldBe("s");
        var measurement = collector.GetMeasurementSnapshot().ShouldHaveSingleItem();
        measurement.Value.ShouldBe(1.5);
        measurement.Tags["outcome"].ShouldBe(expectedOutcome);
    }

    [Theory]
    [InlineData(FetchOutcome.Succeeded, "succeeded")]
    [InlineData(FetchOutcome.Empty, "empty")]
    [InlineData(FetchOutcome.Failed, "failed")]
    public void RecordFetch_AnyOutcome_CountsOneFetchWithOutcome(FetchOutcome outcome, string expectedOutcome)
    {
        // Arrange
        using var collector = Collect<long>(MetricsService.FetchesName);

        // Act
        _sut.RecordFetch(outcome);

        // Assert
        var measurement = collector.GetMeasurementSnapshot().ShouldHaveSingleItem();
        measurement.Value.ShouldBe(1);
        measurement.Tags["outcome"].ShouldBe(expectedOutcome);
    }

    [Fact]
    public void RecordReadingsReceived_ReadingsOfSeveralTypes_CountsReadingsPerReadingType()
    {
        // Arrange
        using var collector = Collect<long>(MetricsService.ReadingsReceivedName);

        // Act
        _sut.RecordReadingsReceived(
        [
            MeterReadings.Motion("Garage"),
            MeterReadings.Motion("Office"),
            MeterReadings.Energy(),
            MeterReadings.AirQuality()
        ]);

        // Assert
        var countsByType = collector.GetMeasurementSnapshot()
            .ToDictionary(measurement => (string)measurement.Tags["pulse.reading.type"]!, measurement => measurement.Value);
        countsByType.ShouldBe(new Dictionary<string, long>
        {
            ["motion"] = 2,
            ["energy"] = 1,
            ["air_quality"] = 1
        });
    }

    [Fact]
    public void RecordReadingsReceived_NoReadings_RecordsNothing()
    {
        // Arrange
        using var collector = Collect<long>(MetricsService.ReadingsReceivedName);

        // Act
        _sut.RecordReadingsReceived([]);

        // Assert
        collector.GetMeasurementSnapshot().ShouldBeEmpty();
    }

    [Theory]
    [InlineData(PublishOutcome.Confirmed, "confirmed")]
    [InlineData(PublishOutcome.TimedOut, "timed_out")]
    [InlineData(PublishOutcome.Returned, "returned")]
    [InlineData(PublishOutcome.Nacked, "nacked")]
    [InlineData(PublishOutcome.Failed, "failed")]
    [InlineData(PublishOutcome.Skipped, "skipped")]
    public void RecordPublish_AnyOutcome_CountsOneMessageWithTypeAndOutcome(PublishOutcome outcome, string expectedOutcome)
    {
        // Arrange
        using var collector = Collect<long>(MetricsService.MessagesPublishedName);

        // Act
        _sut.RecordPublish("MotionReadingReceived", outcome);

        // Assert
        var measurement = collector.GetMeasurementSnapshot().ShouldHaveSingleItem();
        measurement.Value.ShouldBe(1);
        measurement.Tags["pulse.message.type"].ShouldBe("MotionReadingReceived");
        measurement.Tags["outcome"].ShouldBe(expectedOutcome);
    }

    private MetricCollector<T> Collect<T>(string instrumentName)
        where T : struct
    {
        return new MetricCollector<T>(_meterFactory, IngestorTelemetry.ScopeName, instrumentName);
    }
}
