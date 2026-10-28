namespace DataIngestor.Api.Configuration;

internal sealed record OpenTelemetrySettings
{
    public const string SectionName = "OpenTelemetry";

    public string? OtlpEndpoint { get; init; }
}
