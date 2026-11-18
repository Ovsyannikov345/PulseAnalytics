using System.Diagnostics.Metrics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.Metrics.Testing;
using NotificationService.Application.LiveReadings.Enums;
using NotificationService.Application.Telemetry;
using NotificationService.Application.Telemetry.Metrics;
using NotificationService.Application.Telemetry.Metrics.Enums;
using Shouldly;
using Xunit;

namespace NotificationService.UnitTests.Application.Telemetry.Metrics;

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
    [InlineData(ConsumeOutcome.Acked, "acked")]
    [InlineData(ConsumeOutcome.Rejected, "rejected")]
    [InlineData(ConsumeOutcome.Requeued, "requeued")]
    public void RecordMessageProcessed_AnyOutcome_RecordsDurationInSecondsWithMessageTypeAndOutcome(
        ConsumeOutcome outcome,
        string expectedOutcome)
    {
        // Arrange
        using var collector = Collect<double>(MetricsService.MessageProcessingDurationName);

        // Act
        _sut.RecordMessageProcessed("MotionReadingStored", TimeSpan.FromMilliseconds(250), outcome);

        // Assert
        collector.Instrument.ShouldNotBeNull().Unit.ShouldBe("s");
        var measurement = collector.GetMeasurementSnapshot().ShouldHaveSingleItem();
        measurement.Value.ShouldBe(0.25);
        measurement.Tags["app.message.type"].ShouldBe("MotionReadingStored");
        measurement.Tags["app.outcome"].ShouldBe(expectedOutcome);
    }

    [Fact]
    public void RecordMessageProcessed_UnknownOutcome_Throws()
    {
        // Act & Assert
        Should.Throw<System.Diagnostics.UnreachableException>(
            () => _sut.RecordMessageProcessed("MotionReadingStored", TimeSpan.Zero, (ConsumeOutcome)99));
    }

    [Theory]
    [InlineData(MeterType.AirQuality, "air_quality")]
    [InlineData(MeterType.Energy, "energy")]
    [InlineData(MeterType.Motion, "motion")]
    public void RecordBroadcast_AnyReadingType_CountsOneReadingTaggedWithItsType(MeterType meterType, string expectedReadingType)
    {
        // Arrange
        using var collector = Collect<long>(MetricsService.ReadingsBroadcastName);

        // Act
        _sut.RecordBroadcast(meterType, TimeSpan.FromSeconds(1));

        // Assert
        collector.Instrument.ShouldNotBeNull().Unit.ShouldBe("{reading}");
        var measurement = collector.GetMeasurementSnapshot().ShouldHaveSingleItem();
        measurement.Value.ShouldBe(1);
        measurement.Tags["app.reading.type"].ShouldBe(expectedReadingType);
    }

    [Fact]
    public void RecordBroadcast_AnyReading_RecordsDelayInSecondsTaggedWithReadingType()
    {
        // Arrange
        using var collector = Collect<double>(MetricsService.ReadingDelayName);

        // Act
        _sut.RecordBroadcast(MeterType.Energy, TimeSpan.FromMilliseconds(1500));

        // Assert
        collector.Instrument.ShouldNotBeNull().Unit.ShouldBe("s");
        var measurement = collector.GetMeasurementSnapshot().ShouldHaveSingleItem();
        measurement.Value.ShouldBe(1.5);
        measurement.Tags["app.reading.type"].ShouldBe("energy");
    }

    [Fact]
    public void RecordBroadcast_UnknownReadingType_Throws()
    {
        // Act & Assert
        Should.Throw<System.Diagnostics.UnreachableException>(() => _sut.RecordBroadcast((MeterType)99, TimeSpan.Zero));
    }

    private MetricCollector<T> Collect<T>(string instrumentName)
        where T : struct
    {
        return new MetricCollector<T>(_meterFactory, NotificationServiceTelemetry.ScopeName, instrumentName);
    }
}
