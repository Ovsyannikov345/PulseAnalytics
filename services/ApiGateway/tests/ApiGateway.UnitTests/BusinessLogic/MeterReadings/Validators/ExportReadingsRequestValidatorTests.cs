using ApiGateway.BusinessLogic.MeterReadings.Requests;
using ApiGateway.BusinessLogic.MeterReadings.Validators;
using ApiGateway.Common.Constants.Enums;
using FluentValidation.TestHelper;
using Shouldly;
using Xunit;

namespace ApiGateway.UnitTests.BusinessLogic.MeterReadings.Validators;

public sealed class ExportReadingsRequestValidatorTests
{
    private static readonly DateTimeOffset From = new(2026, 11, 4, 10, 0, 0, TimeSpan.Zero);

    private static readonly ExportReadingsRequest ValidRequest = new()
    {
        Types = [MeterType.Energy, MeterType.Motion],
        Locations = [MeterLocation.Kitchen, MeterLocation.Office],
        From = From,
        To = From.AddDays(1)
    };

    private readonly ExportReadingsRequestValidator _sut = new();

    [Fact]
    public void Validate_ValidRequest_HasNoErrors()
    {
        // Act
        var result = _sut.TestValidate(ValidRequest);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_NoTypesOrLocations_HasNoErrors()
    {
        // Act
        var result = _sut.TestValidate(ValidRequest with { Types = null, Locations = [] });

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_UndefinedTypeAndLocation_HasErrors()
    {
        // Act
        var result = _sut.TestValidate(ValidRequest with
        {
            Types = [MeterType.Energy, (MeterType)0],
            Locations = [(MeterLocation)99]
        });

        // Assert
        result.ShouldHaveValidationErrorFor("Types[1]");
        result.ShouldHaveValidationErrorFor("Locations[0]");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Validate_FromNotEarlierThanTo_HasError(int toOffsetSeconds)
    {
        // Act
        var result = _sut.TestValidate(ValidRequest with { To = From.AddSeconds(toOffsetSeconds) });

        // Assert
        var error = result.ShouldHaveValidationErrorFor(r => r.From).Only().ShouldHaveSingleItem();
        error.ErrorMessage.ShouldBe("'From' must be earlier than 'To'.");
    }

    [Fact]
    public void Validate_RangeOfMaxLength_HasNoErrors()
    {
        // Act
        var result = _sut.TestValidate(ValidRequest with { To = From.AddDays(7) });

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_RangeLongerThanMaxLength_HasError()
    {
        // Act
        var result = _sut.TestValidate(ValidRequest with { To = From.AddDays(7).AddTicks(1) });

        // Assert
        var error = result.ShouldHaveValidationErrorFor(r => r.To).Only().ShouldHaveSingleItem();
        error.ErrorMessage.ShouldBe("'To' must be at most 7 days after 'From'.");
    }

    [Fact]
    public void Validate_RangeWithDifferentOffsets_ComparesInstants()
    {
        // Arrange
        // 7 days between the instants, although the local clock times are 7 days and 2 hours apart.
        var to = From.AddDays(7).ToOffset(TimeSpan.FromHours(2));

        // Act
        var result = _sut.TestValidate(ValidRequest with { To = to });

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }
}
