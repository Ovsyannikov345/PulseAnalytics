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

    [Fact]
    public void CommandTimeoutSeconds_NotConfigured_IsTenSeconds()
    {
        // Assert
        ValidSettings.CommandTimeoutSeconds.ShouldBe(10);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(600)]
    public void Validate_CommandTimeoutInRange_Passes(int commandTimeoutSeconds)
    {
        // Act
        var results = DataAnnotationsValidator.Validate(ValidSettings with { CommandTimeoutSeconds = commandTimeoutSeconds });

        // Assert
        results.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(601)]
    public void Validate_CommandTimeoutOutOfRange_Fails(int commandTimeoutSeconds)
    {
        // Act
        var results = DataAnnotationsValidator.Validate(ValidSettings with { CommandTimeoutSeconds = commandTimeoutSeconds });

        // Assert
        results.ShouldHaveSingleItem().MemberNames.ShouldBe([nameof(DatabaseSettings.CommandTimeoutSeconds)]);
    }
}
