using System.Diagnostics;
using System.Diagnostics.Metrics;
using ApiGateway.BusinessLogic.Telemetry.Metrics.Enums;

namespace ApiGateway.BusinessLogic.Telemetry.Metrics;

public interface IMetricsService
{
    public void RecordExport(long rowsWritten, ExportOutcome outcome);
}

internal sealed class MetricsService : IMetricsService
{
    public const string ExportsName = "api_gateway.exports";

    public const string ExportRowsName = "api_gateway.export.rows";

    private static readonly ExportOutcome[] ExportOutcomes = Enum.GetValues<ExportOutcome>();

    private readonly long[] _exports = new long[ExportOutcomes.Length];

    private readonly long[] _exportRows = new long[ExportOutcomes.Length];

    public MetricsService(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create(ApiGatewayTelemetry.ScopeName);

        meter.CreateObservableCounter(
            ExportsName,
            () => Observe(_exports),
            unit: "{export}",
            description: "Number of CSV exports, by outcome.");

        meter.CreateObservableCounter(
            ExportRowsName,
            () => Observe(_exportRows),
            unit: "{row}",
            description: "Number of rows written by CSV exports, by outcome.");
    }

    public void RecordExport(long rowsWritten, ExportOutcome outcome)
    {
        var index = Array.IndexOf(ExportOutcomes, outcome);

        if (index < 0)
        {
            throw new UnreachableException($"Undefined export outcome {outcome}");
        }

        Interlocked.Increment(ref _exports[index]);
        Interlocked.Add(ref _exportRows[index], rowsWritten);
    }

    private static IEnumerable<Measurement<long>> Observe(long[] totals)
    {
        for (var index = 0; index < ExportOutcomes.Length; index++)
        {
            yield return new Measurement<long>(
                Interlocked.Read(ref totals[index]),
                new KeyValuePair<string, object?>(ApiGatewayTelemetry.Tags.Outcome, ToTagValue(ExportOutcomes[index])));
        }
    }

    private static string ToTagValue(ExportOutcome outcome)
    {
        return outcome switch
        {
            ExportOutcome.Completed => "completed",
            ExportOutcome.Cancelled => "cancelled",
            ExportOutcome.Failed => "failed",
            _ => throw new UnreachableException($"No tag value for export outcome {outcome}")
        };
    }
}
