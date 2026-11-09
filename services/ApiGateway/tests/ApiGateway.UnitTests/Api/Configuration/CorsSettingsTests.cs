using ApiGateway.Api.Configuration;
using ApiGateway.UnitTests.Utilities;
using Shouldly;
using Xunit;

namespace ApiGateway.UnitTests.Api.Configuration;

public sealed class CorsSettingsTests
{
    [Fact]
    public void AllowedOrigins_NotConfigured_IsEmpty()
    {
        // Act
        var settings = new CorsSettings();

        // Assert
        settings.AllowedOrigins.ShouldBeEmpty();
        DataAnnotationsValidator.Validate(settings).ShouldBeEmpty();
    }

    [Theory]
    [InlineData("http://localhost:5173")]
    [InlineData("https://dashboard.example.com")]
    [InlineData("HTTP://LOCALHOST:5173")]
    [InlineData("http://127.0.0.1:8080")]
    [InlineData("http://[::1]:5173")]
    public void Validate_Origin_Passes(string origin)
    {
        // Act
        var results = DataAnnotationsValidator.Validate(new CorsSettings { AllowedOrigins = [origin] });

        // Assert
        results.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("http://localhost:5173/")]
    [InlineData("http://localhost:5173/dashboard")]
    [InlineData("http://localhost:5173?x=1")]
    [InlineData("http://localhost:80")]
    [InlineData("http://user@localhost:5173")]
    [InlineData("localhost:5173")]
    [InlineData("ftp://localhost:5173")]
    [InlineData("*")]
    [InlineData("")]
    [InlineData(" ")]
    public void Validate_NotAnOrigin_FailsWithTheValue(string origin)
    {
        // Act
        var results = DataAnnotationsValidator.Validate(new CorsSettings { AllowedOrigins = [origin] });

        // Assert
        var result = results.ShouldHaveSingleItem();
        result.MemberNames.ShouldBe([nameof(CorsSettings.AllowedOrigins)]);
        result.ErrorMessage.ShouldStartWith($"'{origin}' is not an origin.");
    }

    [Fact]
    public void Validate_SeveralInvalidOrigins_ReportsEach()
    {
        // Arrange
        var settings = new CorsSettings { AllowedOrigins = ["http://localhost:5173/", "http://localhost:3001", "localhost:4000"] };

        // Act
        var results = DataAnnotationsValidator.Validate(settings);

        // Assert
        results.Select(result => result.ErrorMessage).ShouldBe(
        [
            "'http://localhost:5173/' is not an origin. Use scheme://host[:port] without a path or trailing slash, e.g. http://localhost:5173.",
            "'localhost:4000' is not an origin. Use scheme://host[:port] without a path or trailing slash, e.g. http://localhost:5173."
        ]);
    }
}
