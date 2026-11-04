using System.ComponentModel.DataAnnotations;

namespace ApiGateway.DataAccess.Database.Settings;

internal sealed record DatabaseSettings
{
    public const string SectionName = "Database";

    [Required(AllowEmptyStrings = false)]
    public required string ConnectionString { get; init; }
}
