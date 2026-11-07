using ApiGateway.BusinessLogic.MeterReadings.Requests;
using ApiGateway.BusinessLogic.MeterReadings.Validators;
using ApiGateway.BusinessLogic.Paging;
using ApiGateway.Common.Constants.Enums;
using FluentValidation.TestHelper;
using Shouldly;
using Xunit;

namespace ApiGateway.UnitTests.BusinessLogic.MeterReadings.Validators;

public sealed class GetAllReadingsRequestValidatorTests
{
    private static readonly DateTimeOffset From = new(2026, 11, 4, 10, 0, 0, TimeSpan.Zero);

    private static readonly GetAllReadingsRequest ValidRequest = new()
    {
        Types = [MeterType.Energy, MeterType.Motion],
        Locations = [MeterLocation.Kitchen],
        From = From,
        To = From.AddHours(1),
        PageSize = 10
    };

    private readonly GetAllReadingsRequestValidator _sut = new();

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
        var result = _sut.TestValidate(new GetAllReadingsRequest());

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_UndefinedType_HasError()
    {
        // Act
        var result = _sut.TestValidate(ValidRequest with { Types = [MeterType.Energy, (MeterType)99] });

        // Assert
        result.ShouldHaveValidationErrorFor("Types[1]").Only();
    }

    [Fact]
    public void Validate_UndefinedLocation_HasError()
    {
        // Act
        var result = _sut.TestValidate(ValidRequest with { Locations = [(MeterLocation)0] });

        // Assert
        result.ShouldHaveValidationErrorFor("Locations[0]").Only();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Validate_FromNotEarlierThanTo_HasError(int minutesFromStart)
    {
        // Act
        var result = _sut.TestValidate(ValidRequest with { To = From.AddMinutes(minutesFromStart) });

        // Assert
        var error = result.ShouldHaveValidationErrorFor(r => r.From).Only().ShouldHaveSingleItem();
        error.ErrorMessage.ShouldBe("'From' must be earlier than 'To'.");
    }

    [Fact]
    public void Validate_FromAndToInDifferentOffsets_ComparesInstants()
    {
        // Arrange
        // 11:30+02:00 is 09:30 UTC, half an hour before From.
        var request = ValidRequest with { To = new DateTimeOffset(2026, 11, 4, 11, 30, 0, TimeSpan.FromHours(2)) };

        // Act
        var result = _sut.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(r => r.From);
    }

    [Fact]
    public void Validate_OnlyOneBound_HasNoErrors()
    {
        // Act
        var onlyFrom = _sut.TestValidate(ValidRequest with { To = null });
        var onlyTo = _sut.TestValidate(ValidRequest with { From = null });

        // Assert
        onlyFrom.ShouldNotHaveAnyValidationErrors();
        onlyTo.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData(null)]
    [InlineData(1)]
    [InlineData(50)]
    [InlineData(PagingLimits.MaxPageSize)]
    public void Validate_PageSizeOmittedOrInRange_HasNoErrors(int? pageSize)
    {
        // Act
        var result = _sut.TestValidate(ValidRequest with { PageSize = pageSize });

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(PagingLimits.MaxPageSize + 1)]
    public void Validate_PageSizeOutOfRange_HasError(int pageSize)
    {
        // Act
        var result = _sut.TestValidate(ValidRequest with { PageSize = pageSize });

        // Assert
        result.ShouldHaveValidationErrorFor(r => r.PageSize).Only();
    }
}
