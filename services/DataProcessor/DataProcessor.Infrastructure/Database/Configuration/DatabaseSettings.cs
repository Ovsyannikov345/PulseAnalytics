using System.ComponentModel.DataAnnotations;

namespace DataProcessor.Infrastructure.Database.Configuration;

internal sealed record DatabaseSettings
{
    public const string SectionName = "Database";

    [Required(AllowEmptyStrings = false)]
    public required string ConnectionString { get; init; }
}
