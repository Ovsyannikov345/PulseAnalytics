using ApiGateway.BusinessLogic.MeterReadings.Models;
using ApiGateway.BusinessLogic.MeterReadings.Requests;
using ApiGateway.BusinessLogic.MeterReadings.Validators;
using ApiGateway.Common.Constants.Enums;
using FluentValidation.TestHelper;
using Shouldly;
using Xunit;

namespace ApiGateway.UnitTests.BusinessLogic.MeterReadings.Validators;

public sealed class GetReadingCountsRequestValidatorTests
{
    private static readonly DateTimeOffset From = new(2026, 11, 4, 10, 0, 0, TimeSpan.Zero);

    private static readonly GetReadingCountsRequest ValidRequest = new()
    {
        Types = [MeterType.Energy],
        Locations = [MeterLocation.Kitchen, MeterLocation.Office],
        From = From,
        To = From.AddHours(1),
        GroupBy = [ReadingGrouping.Location, ReadingGrouping.Type]
    };

    private readonly GetReadingCountsRequestValidator _sut = new();

    [Fact]
    public void Validate_ValidRequest_HasNoErrors()
    {
        // Act
        var result = _sut.TestValidate(ValidRequest);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_EmptyRequest_HasNoErrors()
    {
        // Act
        var result = _sut.TestValidate(new GetReadingCountsRequest());

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_UndefinedTypeAndLocation_HasErrors()
    {
        // Act
        var result = _sut.TestValidate(ValidRequest with { Types = [(MeterType)99], Locations = [(MeterLocation)0] });

        // Assert
        result.ShouldHaveValidationErrorFor("Types[0]");
        result.ShouldHaveValidationErrorFor("Locations[0]");
    }

    [Fact]
    public void Validate_FromNotEarlierThanTo_HasError()
    {
        // Act
        var result = _sut.TestValidate(ValidRequest with { To = From.AddHours(-1) });

        // Assert
        var error = result.ShouldHaveValidationErrorFor(r => r.From).Only().ShouldHaveSingleItem();
        error.ErrorMessage.ShouldBe("'From' must be earlier than 'To'.");
    }

    [Fact]
    public void Validate_UndefinedGrouping_HasError()
    {
        // Act
        var result = _sut.TestValidate(ValidRequest with { GroupBy = [ReadingGrouping.Type, (ReadingGrouping)9] });

        // Assert
        result.ShouldHaveValidationErrorFor("GroupBy[1]").Only();
    }
}
