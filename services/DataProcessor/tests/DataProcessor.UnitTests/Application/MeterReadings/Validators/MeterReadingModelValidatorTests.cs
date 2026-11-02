using DataProcessor.Application.MeterReadings.Models;
using DataProcessor.Application.MeterReadings.Validators;
using DataProcessor.Domain.Constants.Enums;
using FluentValidation.TestHelper;
using Microsoft.Extensions.Time.Testing;
using Shouldly;
using Xunit;

namespace DataProcessor.UnitTests.Application.MeterReadings.Validators;

// Covers the shared rules of MeterReadingModelValidator through MotionReadingModelValidator, which adds none.
public sealed class MeterReadingModelValidatorTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 1, 12, 0, 0, TimeSpan.Zero);

    private static readonly MotionReadingModel ValidModel = new()
    {
        DeduplicationKey = Guid.Parse("0199f5c4-3a1e-7b52-9d0e-5c8a7e6b4f21"),
        Location = MeterLocation.Garage,
        ReceivedAt = Now,
        MotionDetected = true
    };

    private readonly MotionReadingModelValidator _sut = new(new FakeTimeProvider(Now));

    [Fact]
    public void Validate_ValidReading_HasNoErrors()
    {
        // Act
        var result = _sut.TestValidate(ValidModel);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_EmptyDeduplicationKey_HasError()
    {
        // Act
        var result = _sut.TestValidate(ValidModel with { DeduplicationKey = Guid.Empty });

        // Assert
        result.ShouldHaveValidationErrorFor(m => m.DeduplicationKey).Only();
    }

    [Fact]
    public void Validate_UndefinedLocation_HasError()
    {
        // Act
        var result = _sut.TestValidate(ValidModel with { Location = (MeterLocation)99 });

        // Assert
        result.ShouldHaveValidationErrorFor(m => m.Location).Only();
    }

    [Fact]
    public void Validate_DefaultReceivedAt_HasError()
    {
        // Act
        var result = _sut.TestValidate(ValidModel with { ReceivedAt = default });

        // Assert
        result.ShouldHaveValidationErrorFor(m => m.ReceivedAt).Only();
    }

    [Theory]
    [InlineData(-86_400)]
    [InlineData(0)]
    [InlineData(30)]
    [InlineData(60)]
    public void Validate_ReceivedAtInPastOrWithinClockSkew_HasNoErrors(int secondsFromNow)
    {
        // Act
        var result = _sut.TestValidate(ValidModel with { ReceivedAt = Now.AddSeconds(secondsFromNow) });

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_ReceivedAtBeyondClockSkew_HasFutureError()
    {
        // Act
        var result = _sut.TestValidate(ValidModel with { ReceivedAt = Now.AddSeconds(61) });

        // Assert
        var error = result.ShouldHaveValidationErrorFor(m => m.ReceivedAt).Only().ShouldHaveSingleItem();
        error.ErrorMessage.ShouldBe("'Received At' must not be in the future.");
    }
}
