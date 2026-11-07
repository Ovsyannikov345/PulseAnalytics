using ApiGateway.BusinessLogic.MeterReadings.Requests;
using ApiGateway.BusinessLogic.MeterReadings.Validators;
using ApiGateway.Common.Constants.Enums;
using FluentValidation.TestHelper;
using Xunit;

namespace ApiGateway.UnitTests.BusinessLogic.MeterReadings.Validators;

public sealed class GetLatestReadingsRequestValidatorTests
{
    private readonly GetLatestReadingsRequestValidator _sut = new();

    [Fact]
    public void Validate_EmptyRequest_HasNoErrors()
    {
        // Act
        var result = _sut.TestValidate(new GetLatestReadingsRequest());

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_DefinedValues_HasNoErrors()
    {
        // Act
        var result = _sut.TestValidate(new GetLatestReadingsRequest
        {
            Types = [MeterType.AirQuality],
            Locations = [MeterLocation.Garage, MeterLocation.Office]
        });

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_UndefinedValues_HasErrors()
    {
        // Act
        var result = _sut.TestValidate(new GetLatestReadingsRequest
        {
            Types = [(MeterType)4],
            Locations = [(MeterLocation)7]
        });

        // Assert
        result.ShouldHaveValidationErrorFor("Types[0]");
        result.ShouldHaveValidationErrorFor("Locations[0]");
    }
}
