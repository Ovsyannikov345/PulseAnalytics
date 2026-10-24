using DataIngestor.Infrastructure.WeakApp.Configuration;
using DataIngestor.UnitTests.Utilities;
using Shouldly;
using Xunit;

namespace DataIngestor.UnitTests.Infrastructure.WeakApp.Configuration;

public sealed class WeakAppSettingsTests
{
    private const string ValidBaseUrl = "http://weakapp.test";
    private const string ValidApiKey = "test-api-key";

    [Theory]
    [InlineData("http://weakapp.test")]
    [InlineData("https://weakapp.test/api/")]
    public void Validate_AbsoluteHttpUrlAndApiKey_Passes(string baseUrl)
    {
        // Arrange
        var settings = new WeakAppSettings { BaseUrl = baseUrl, ApiKey = ValidApiKey };

        // Act
        var results = DataAnnotationsValidator.Validate(settings);

        // Assert
        results.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-a-url")]
    [InlineData("weakapp.test")]
    public void Validate_MissingOrInvalidBaseUrl_Fails(string? baseUrl)
    {
        // Arrange
        // Configuration binding leaves a missing value null despite the non-nullable property.
        var settings = new WeakAppSettings { BaseUrl = baseUrl!, ApiKey = ValidApiKey };

        // Act
        var results = DataAnnotationsValidator.Validate(settings);

        // Assert
        results.SelectMany(result => result.MemberNames).Distinct().ShouldBe([nameof(WeakAppSettings.BaseUrl)]);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Validate_MissingApiKey_Fails(string? apiKey)
    {
        // Arrange
        var settings = new WeakAppSettings { BaseUrl = ValidBaseUrl, ApiKey = apiKey! };

        // Act
        var results = DataAnnotationsValidator.Validate(settings);

        // Assert
        results.ShouldHaveSingleItem().MemberNames.ShouldBe([nameof(WeakAppSettings.ApiKey)]);
    }
}
