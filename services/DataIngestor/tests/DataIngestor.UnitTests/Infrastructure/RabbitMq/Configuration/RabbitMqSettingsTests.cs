using DataIngestor.Infrastructure.RabbitMq.Configuration;
using DataIngestor.UnitTests.Utilities;
using Shouldly;
using Xunit;

namespace DataIngestor.UnitTests.Infrastructure.RabbitMq.Configuration;

public sealed class RabbitMqSettingsTests
{
    private static readonly RabbitMqSettings ValidSettings = new()
    {
        Host = "rabbitmq.test",
        Port = 5672,
        VirtualHost = "/",
        UserName = "ingestor",
        Password = "secret",
        ClientProvidedName = "pulse-analytics.data-ingestor",
        PublishTimeoutInSeconds = 5
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
    public void Validate_MissingHost_Fails(string? host)
    {
        // Arrange
        // Configuration binding leaves a missing value null despite the non-nullable property.
        var settings = ValidSettings with { Host = host! };

        // Act
        var results = DataAnnotationsValidator.Validate(settings);

        // Assert
        results.ShouldHaveSingleItem().MemberNames.ShouldBe([nameof(RabbitMqSettings.Host)]);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Validate_MissingVirtualHost_Fails(string? virtualHost)
    {
        // Arrange
        var settings = ValidSettings with { VirtualHost = virtualHost! };

        // Act
        var results = DataAnnotationsValidator.Validate(settings);

        // Assert
        results.ShouldHaveSingleItem().MemberNames.ShouldBe([nameof(RabbitMqSettings.VirtualHost)]);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Validate_MissingUserName_Fails(string? userName)
    {
        // Arrange
        var settings = ValidSettings with { UserName = userName! };

        // Act
        var results = DataAnnotationsValidator.Validate(settings);

        // Assert
        results.ShouldHaveSingleItem().MemberNames.ShouldBe([nameof(RabbitMqSettings.UserName)]);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Validate_MissingPassword_Fails(string? password)
    {
        // Arrange
        var settings = ValidSettings with { Password = password! };

        // Act
        var results = DataAnnotationsValidator.Validate(settings);

        // Assert
        results.ShouldHaveSingleItem().MemberNames.ShouldBe([nameof(RabbitMqSettings.Password)]);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Validate_MissingClientProvidedName_Fails(string? clientProvidedName)
    {
        // Arrange
        var settings = ValidSettings with { ClientProvidedName = clientProvidedName! };

        // Act
        var results = DataAnnotationsValidator.Validate(settings);

        // Assert
        results.ShouldHaveSingleItem().MemberNames.ShouldBe([nameof(RabbitMqSettings.ClientProvidedName)]);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(65535)]
    public void Validate_PortWithinRange_Passes(int port)
    {
        // Arrange
        var settings = ValidSettings with { Port = port };

        // Act
        var results = DataAnnotationsValidator.Validate(settings);

        // Assert
        results.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(0)] // A missing Port binds to 0
    [InlineData(65536)]
    public void Validate_PortOutOfRange_Fails(int port)
    {
        // Arrange
        var settings = ValidSettings with { Port = port };

        // Act
        var results = DataAnnotationsValidator.Validate(settings);

        // Assert
        results.ShouldHaveSingleItem().MemberNames.ShouldBe([nameof(RabbitMqSettings.Port)]);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(60)]
    public void Validate_PublishTimeoutWithinRange_Passes(int publishTimeoutInSeconds)
    {
        // Arrange
        var settings = ValidSettings with { PublishTimeoutInSeconds = publishTimeoutInSeconds };

        // Act
        var results = DataAnnotationsValidator.Validate(settings);

        // Assert
        results.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(0)] // A missing PublishTimeoutInSeconds binds to 0
    [InlineData(61)]
    public void Validate_PublishTimeoutOutOfRange_Fails(int publishTimeoutInSeconds)
    {
        // Arrange
        var settings = ValidSettings with { PublishTimeoutInSeconds = publishTimeoutInSeconds };

        // Act
        var results = DataAnnotationsValidator.Validate(settings);

        // Assert
        results.ShouldHaveSingleItem().MemberNames.ShouldBe([nameof(RabbitMqSettings.PublishTimeoutInSeconds)]);
    }
}
