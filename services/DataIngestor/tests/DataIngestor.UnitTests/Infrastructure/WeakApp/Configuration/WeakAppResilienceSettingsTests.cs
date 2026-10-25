using DataIngestor.Infrastructure.WeakApp.Configuration;
using DataIngestor.UnitTests.Utilities;
using Shouldly;
using Xunit;

namespace DataIngestor.UnitTests.Infrastructure.WeakApp.Configuration;

public sealed class WeakAppResilienceSettingsTests
{
    private static readonly WeakAppResilienceSettings ValidSettings = new()
    {
        TotalTimeoutInSeconds = 20,
        AttemptTimeoutInSeconds = 5,
        MaxRetryAttempts = 2,
        RetryBaseDelayInSeconds = 1
    };

    [Fact]
    public void Validate_ValidSettings_Passes()
    {
        // Act
        var results = DataAnnotationsValidator.Validate(ValidSettings);

        // Assert
        results.ShouldBeEmpty();
    }

    [Fact]
    public void Validate_ZeroTotalTimeout_Fails()
    {
        // Arrange
        var settings = ValidSettings with { TotalTimeoutInSeconds = 0 };

        // Act
        var results = DataAnnotationsValidator.Validate(settings);

        // Assert
        results.ShouldHaveSingleItem().MemberNames.ShouldBe([nameof(WeakAppResilienceSettings.TotalTimeoutInSeconds)]);
    }

    [Fact]
    public void Validate_ZeroAttemptTimeout_Fails()
    {
        // Arrange
        var settings = ValidSettings with { AttemptTimeoutInSeconds = 0 };

        // Act
        var results = DataAnnotationsValidator.Validate(settings);

        // Assert
        results.ShouldHaveSingleItem().MemberNames.ShouldBe([nameof(WeakAppResilienceSettings.AttemptTimeoutInSeconds)]);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    public void Validate_MaxRetryAttemptsOutOfRange_Fails(int maxRetryAttempts)
    {
        // Arrange
        var settings = ValidSettings with { MaxRetryAttempts = maxRetryAttempts };

        // Act
        var results = DataAnnotationsValidator.Validate(settings);

        // Assert
        results.ShouldHaveSingleItem().MemberNames.ShouldBe([nameof(WeakAppResilienceSettings.MaxRetryAttempts)]);
    }

    [Fact]
    public void Validate_ZeroRetryBaseDelay_Fails()
    {
        // Arrange
        var settings = ValidSettings with { RetryBaseDelayInSeconds = 0 };

        // Act
        var results = DataAnnotationsValidator.Validate(settings);

        // Assert
        results.ShouldHaveSingleItem().MemberNames.ShouldBe([nameof(WeakAppResilienceSettings.RetryBaseDelayInSeconds)]);
    }

    [Theory]
    [InlineData(5, 5)]
    [InlineData(10, 5)]
    public void Validate_AttemptTimeoutNotLessThanTotalTimeout_Fails(int attemptTimeoutInSeconds, int totalTimeoutInSeconds)
    {
        // Arrange
        var settings = ValidSettings with
        {
            AttemptTimeoutInSeconds = attemptTimeoutInSeconds,
            TotalTimeoutInSeconds = totalTimeoutInSeconds
        };

        // Act
        var results = DataAnnotationsValidator.Validate(settings);

        // Assert
        results.ShouldHaveSingleItem().MemberNames.ShouldBe(
        [
            nameof(WeakAppResilienceSettings.AttemptTimeoutInSeconds),
            nameof(WeakAppResilienceSettings.TotalTimeoutInSeconds)
        ]);
    }
}
