namespace DataIngestor.Api.Configuration;

internal sealed record SeqSettings
{
    public const string SectionName = "Seq";

    public string? ServerUrl { get; init; }

    public string? ApiKey { get; init; }
}
