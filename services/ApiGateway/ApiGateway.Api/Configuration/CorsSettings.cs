using System.ComponentModel.DataAnnotations;

namespace ApiGateway.Api.Configuration;

internal sealed record CorsSettings : IValidatableObject
{
    public const string SectionName = "Cors";

    public string[] AllowedOrigins { get; init; } = [];

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        return AllowedOrigins
            .Where(origin => !IsSerializedOrigin(origin))
            .Select(origin => new ValidationResult(
                $"'{origin}' is not an origin. Use scheme://host[:port] without a path or trailing slash, e.g. http://localhost:5173.",
                [nameof(AllowedOrigins)]));
    }

    private static bool IsSerializedOrigin(string? origin)
    {
        return Uri.TryCreate(origin, UriKind.Absolute, out var uri)
               && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
               && string.Equals(origin, uri.GetComponents(UriComponents.SchemeAndServer, UriFormat.UriEscaped), StringComparison.OrdinalIgnoreCase);
    }
}
