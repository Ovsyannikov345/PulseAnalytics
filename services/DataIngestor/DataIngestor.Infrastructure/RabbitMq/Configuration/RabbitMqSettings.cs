using System.ComponentModel.DataAnnotations;

namespace DataIngestor.Infrastructure.RabbitMq.Configuration;

internal sealed record RabbitMqSettings
{
    public const string SectionName = "RabbitMq";

    [Required(AllowEmptyStrings = false)]
    public required string Host { get; init; }

    [Range(1, 65535)]
    public int Port { get; init; } = 5672;

    [Required(AllowEmptyStrings = false)]
    public string VirtualHost { get; init; } = "/";

    [Required(AllowEmptyStrings = false)]
    public required string UserName { get; init; }

    [Required(AllowEmptyStrings = false)]
    public required string Password { get; init; }

    [Range(1, 60)]
    public int PublishTimeoutInSeconds { get; init; } = 5;
}
