using System.Diagnostics;
using System.Diagnostics.Metrics;
using ApiGateway.BusinessLogic.Telemetry;
using ApiGateway.BusinessLogic.Telemetry.Metrics;
using ApiGateway.BusinessLogic.Telemetry.Metrics.Enums;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.Metrics.Testing;
using Shouldly;
using Xunit;

namespace ApiGateway.UnitTests.BusinessLogic.Telemetry.Metrics;

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
    [InlineData(MetricsService.ExportsName, "{export}")]
    [InlineData(MetricsService.ExportRowsName, "{row}")]
    public void Observe_NoExportYet_ReportsEveryOutcomeAtZero(string instrumentName, string expectedUnit)
    {
        // Arrange
        using var collector = Collect(instrumentName);

        // Act
        var totals = Observe(collector);

        // Assert
        collector.Instrument.ShouldNotBeNull().Unit.ShouldBe(expectedUnit);
        totals.ShouldBe(new Dictionary<string, long> { ["completed"] = 0, ["cancelled"] = 0, ["failed"] = 0 });
    }

    [Theory]
    [InlineData(ExportOutcome.Completed, "completed")]
    [InlineData(ExportOutcome.Cancelled, "cancelled")]
    [InlineData(ExportOutcome.Failed, "failed")]
    public void RecordExport_AnyOutcome_CountsOneExportForThatOutcomeOnly(ExportOutcome outcome, string expectedOutcome)
    {
        // Arrange
        using var collector = Collect(MetricsService.ExportsName);

        // Act
        _sut.RecordExport(1_234, outcome);

        // Assert
        var totals = Observe(collector);
        totals[expectedOutcome].ShouldBe(1);
        totals.Where(total => total.Key != expectedOutcome).ShouldAllBe(total => total.Value == 0);
    }

    [Theory]
    [InlineData(ExportOutcome.Completed, "completed")]
    [InlineData(ExportOutcome.Cancelled, "cancelled")]
    [InlineData(ExportOutcome.Failed, "failed")]
    public void RecordExport_AnyOutcome_AddsRowsWrittenToThatOutcomeOnly(ExportOutcome outcome, string expectedOutcome)
    {
        // Arrange
        using var collector = Collect(MetricsService.ExportRowsName);

        // Act
        _sut.RecordExport(1_234, outcome);

        // Assert
        var totals = Observe(collector);
        totals[expectedOutcome].ShouldBe(1_234);
        totals.Where(total => total.Key != expectedOutcome).ShouldAllBe(total => total.Value == 0);
    }

    [Fact]
    public void RecordExport_SeveralExports_ReportsRunningTotals()
    {
        // Arrange
        using var exports = Collect(MetricsService.ExportsName);
        using var rows = Collect(MetricsService.ExportRowsName);

        // Act
        _sut.RecordExport(100, ExportOutcome.Completed);
        _sut.RecordExport(250, ExportOutcome.Completed);
        _sut.RecordExport(0, ExportOutcome.Failed);

        // Assert
        Observe(exports).ShouldBe(new Dictionary<string, long> { ["completed"] = 2, ["cancelled"] = 0, ["failed"] = 1 });
        Observe(rows).ShouldBe(new Dictionary<string, long> { ["completed"] = 350, ["cancelled"] = 0, ["failed"] = 0 });
    }

    [Fact]
    public void RecordExport_UndefinedOutcome_ThrowsUnreachableException()
    {
        // Act
        var act = () => _sut.RecordExport(1, (ExportOutcome)int.MaxValue);

        // Assert
        act.ShouldThrow<UnreachableException>();
    }

    private static Dictionary<string, long> Observe(MetricCollector<long> collector)
    {
        collector.Clear();
        collector.RecordObservableInstruments();

        return collector
            .GetMeasurementSnapshot()
            .ToDictionary(measurement => (string)measurement.Tags["app.outcome"]!, measurement => measurement.Value);
    }

    private MetricCollector<long> Collect(string instrumentName)
    {
        return new MetricCollector<long>(_meterFactory, ApiGatewayTelemetry.ScopeName, instrumentName);
    }
}
