using DataProcessor.Application.MeterReadings.Models;
using DataProcessor.Application.MeterReadings.Validators;
using DataProcessor.Domain.Constants.Enums;
using DataProcessor.Domain.Constants.Limits;
using FluentValidation.TestHelper;
using Microsoft.Extensions.Time.Testing;
using Shouldly;
using Xunit;

namespace DataProcessor.UnitTests.Application.MeterReadings.Validators;

public sealed class EnergyReadingModelValidatorTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 1, 12, 0, 0, TimeSpan.Zero);

    private static readonly EnergyReadingModel ValidModel = new()
    {
        DeduplicationKey = Guid.Parse("0199f5c4-3a1e-7b52-9d0e-5c8a7e6b4f21"),
        Location = MeterLocation.Office,
        ReceivedAt = Now,
        Energy = 845.62
    };

    private readonly EnergyReadingModelValidator _sut = new(new FakeTimeProvider(Now));

    [Theory]
    [InlineData(EnergyLimits.MinEnergy)]
    [InlineData(845.62)]
    public void Validate_EnergyWithinBounds_HasNoErrors(double energy)
    {
        // Act
        var result = _sut.TestValidate(ValidModel with { Energy = energy });

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData(EnergyLimits.MinEnergy - 0.01)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void Validate_EnergyNegativeOrNotFinite_HasSingleError(double energy)
    {
        // Act
        var result = _sut.TestValidate(ValidModel with { Energy = energy });

        // Assert
        result.ShouldHaveValidationErrorFor(m => m.Energy).Only().ShouldHaveSingleItem();
    }

    [Fact]
    public void Validate_EnergyNotFinite_HasFiniteNumberError()
    {
        // Act
        var result = _sut.TestValidate(ValidModel with { Energy = double.NaN });

        // Assert
        result.ShouldHaveValidationErrorFor(m => m.Energy)
            .WithErrorMessage("'Energy' must be a finite number.");
    }

    [Fact]
    public void Validate_InvalidSharedField_HasError()
    {
        // Act
        var result = _sut.TestValidate(ValidModel with { DeduplicationKey = Guid.Empty });

        // Assert
        result.ShouldHaveValidationErrorFor(m => m.DeduplicationKey).Only();
    }
}
