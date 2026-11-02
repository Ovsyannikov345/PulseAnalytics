using System.Diagnostics;
using System.Diagnostics.Metrics;
using DataProcessor.Application.Telemetry;
using DataProcessor.Application.Telemetry.Metrics;
using DataProcessor.Application.Telemetry.Metrics.Enums;
using DataProcessor.Domain.Constants.Enums;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.Metrics.Testing;
using Shouldly;
using Xunit;

namespace DataProcessor.UnitTests.Application.Telemetry.Metrics;

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
        _sut.RecordMessageProcessed("MotionReadingReceived", TimeSpan.FromMilliseconds(250), outcome);

        // Assert
        collector.Instrument.ShouldNotBeNull().Unit.ShouldBe("s");
        var measurement = collector.GetMeasurementSnapshot().ShouldHaveSingleItem();
        measurement.Value.ShouldBe(0.25);
        measurement.Tags["app.message.type"].ShouldBe("MotionReadingReceived");
        measurement.Tags["app.outcome"].ShouldBe(expectedOutcome);
    }

    [Theory]
    [InlineData(ReadingOutcome.Stored, "stored")]
    [InlineData(ReadingOutcome.Duplicate, "duplicate")]
    [InlineData(ReadingOutcome.Invalid, "invalid")]
    public void RecordReading_AnyOutcome_CountsOneReadingWithOutcome(ReadingOutcome outcome, string expectedOutcome)
    {
        // Arrange
        using var collector = Collect<long>(MetricsService.ReadingsProcessedName);

        // Act
        _sut.RecordReading(MeterType.Energy, MeterLocation.Office, outcome);

        // Assert
        var measurement = collector.GetMeasurementSnapshot().ShouldHaveSingleItem();
        measurement.Value.ShouldBe(1);
        measurement.Tags["app.outcome"].ShouldBe(expectedOutcome);
    }

    [Theory]
    [InlineData(MeterType.AirQuality, "air_quality")]
    [InlineData(MeterType.Energy, "energy")]
    [InlineData(MeterType.Motion, "motion")]
    public void RecordReading_AnyReadingType_TagsReadingType(MeterType meterType, string expectedReadingType)
    {
        // Arrange
        using var collector = Collect<long>(MetricsService.ReadingsProcessedName);

        // Act
        _sut.RecordReading(meterType, MeterLocation.Kitchen, ReadingOutcome.Stored);

        // Assert
        collector.GetMeasurementSnapshot().ShouldHaveSingleItem().Tags["app.reading.type"].ShouldBe(expectedReadingType);
    }

    [Theory]
    [InlineData(MeterLocation.Kitchen, "kitchen")]
    [InlineData(MeterLocation.LivingRoom, "living_room")]
    [InlineData(MeterLocation.Bedroom, "bedroom")]
    [InlineData(MeterLocation.Garage, "garage")]
    [InlineData(MeterLocation.Office, "office")]
    [InlineData(MeterLocation.Corridor, "corridor")]
    public void RecordReading_AnyLocation_TagsLocation(MeterLocation location, string expectedLocation)
    {
        // Arrange
        using var collector = Collect<long>(MetricsService.ReadingsProcessedName);

        // Act
        _sut.RecordReading(MeterType.Motion, location, ReadingOutcome.Stored);

        // Assert
        collector.GetMeasurementSnapshot().ShouldHaveSingleItem().Tags["app.reading.location"].ShouldBe(expectedLocation);
    }

    [Fact]
    public void RecordMessageProcessed_UndefinedOutcome_ThrowsUnreachableException()
    {
        // Act
        var act = () => _sut.RecordMessageProcessed("MotionReadingReceived", TimeSpan.FromSeconds(1), (ConsumeOutcome)int.MaxValue);

        // Assert
        act.ShouldThrow<UnreachableException>();
    }

    [Fact]
    public void RecordReading_UndefinedOutcome_ThrowsUnreachableException()
    {
        // Act
        var act = () => _sut.RecordReading(MeterType.Motion, MeterLocation.Garage, (ReadingOutcome)int.MaxValue);

        // Assert
        act.ShouldThrow<UnreachableException>();
    }

    [Fact]
    public void RecordReading_UndefinedReadingType_ThrowsUnreachableException()
    {
        // Act
        var act = () => _sut.RecordReading((MeterType)int.MaxValue, MeterLocation.Garage, ReadingOutcome.Stored);

        // Assert
        act.ShouldThrow<UnreachableException>();
    }

    [Fact]
    public void RecordReading_UndefinedLocation_ThrowsUnreachableException()
    {
        // Act
        var act = () => _sut.RecordReading(MeterType.Motion, (MeterLocation)int.MaxValue, ReadingOutcome.Stored);

        // Assert
        act.ShouldThrow<UnreachableException>();
    }

    private MetricCollector<T> Collect<T>(string instrumentName)
        where T : struct
    {
        return new MetricCollector<T>(_meterFactory, ProcessorTelemetry.ScopeName, instrumentName);
    }
}
