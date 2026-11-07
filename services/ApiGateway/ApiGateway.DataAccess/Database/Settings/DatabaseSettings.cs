using System.ComponentModel.DataAnnotations;

namespace ApiGateway.DataAccess.Database.Settings;

internal sealed record DatabaseSettings
{
    public const string SectionName = "Database";

    [Required(AllowEmptyStrings = false)]
    public required string ConnectionString { get; init; }

    [Range(1, 600)]
    public int CommandTimeoutSeconds { get; init; } = 10;

    [Range(1, 60)]
    public int ConnectTimeoutSeconds { get; init; } = 3;
}
