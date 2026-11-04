using ApiGateway.DataAccess.Database.Settings;
using ApiGateway.UnitTests.Utilities;
using Shouldly;
using Xunit;

namespace ApiGateway.UnitTests.DataAccess.Database.Configuration;

public sealed class DatabaseSettingsTests
{
    private static readonly DatabaseSettings ValidSettings = new()
    {
        ConnectionString = "Host=postgres.test;Database=pulse_analytics"
    };

    [Fact]
    public void Validate_AllValuesValid_Passes()
    {
        // Act
        var results = DataAnnotationsValidator.Validate(ValidSettings);

        // Assert
        results.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Validate_MissingConnectionString_Fails(string? connectionString)
    {
        // Arrange
        // Configuration binding leaves a missing value null despite the non-nullable property.
        var settings = ValidSettings with { ConnectionString = connectionString! };

        // Act
        var results = DataAnnotationsValidator.Validate(settings);

        // Assert
        results.ShouldHaveSingleItem().MemberNames.ShouldBe([nameof(DatabaseSettings.ConnectionString)]);
    }
}
