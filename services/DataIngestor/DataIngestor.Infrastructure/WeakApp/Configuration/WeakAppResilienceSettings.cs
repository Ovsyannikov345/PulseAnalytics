using System.ComponentModel.DataAnnotations;

namespace DataIngestor.Infrastructure.WeakApp.Configuration;

internal sealed record WeakAppResilienceSettings : IValidatableObject
{
    public const string SectionName = "WeakApp:Resilience";

    [Range(1, 300)]
    public int TotalTimeoutInSeconds { get; init; }

    [Range(1, 60)]
    public int AttemptTimeoutInSeconds { get; init; }

    [Range(1, 5)]
    public int MaxRetryAttempts { get; init; }

    [Range(1, 60)]
    public int RetryBaseDelayInSeconds { get; init; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (AttemptTimeoutInSeconds >= TotalTimeoutInSeconds)
        {
            yield return new ValidationResult(
                $"{nameof(AttemptTimeoutInSeconds)} must be less than {nameof(TotalTimeoutInSeconds)}",
                [nameof(AttemptTimeoutInSeconds), nameof(TotalTimeoutInSeconds)]);
        }
    }
}
