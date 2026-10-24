using System.ComponentModel.DataAnnotations;

namespace DataIngestor.Infrastructure.WeakApp.Configuration;

internal sealed record WeakAppSettings
{
    public const string SectionName = "WeakApp";

    [Url]
    [Required(AllowEmptyStrings = false)]
    public required string BaseUrl { get; init; }

    [Required(AllowEmptyStrings = false)]
    public required string ApiKey { get; init; }
}
