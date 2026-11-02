using DataProcessor.Application.MeterReadings.Models;
using DataProcessor.Application.MeterReadings.Validators;
using DataProcessor.Domain.Constants.Enums;
using DataProcessor.Domain.Constants.Limits;
using FluentValidation.TestHelper;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace DataProcessor.UnitTests.Application.MeterReadings.Validators;

public sealed class AirQualityReadingModelValidatorTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 1, 12, 0, 0, TimeSpan.Zero);

    private static readonly AirQualityReadingModel ValidModel = new()
    {
        DeduplicationKey = Guid.Parse("0199f5c4-3a1e-7b52-9d0e-5c8a7e6b4f21"),
        Location = MeterLocation.Kitchen,
        ReceivedAt = Now,
        Co2 = 803,
        Pm25 = 25,
        Humidity = 36
    };

    private readonly AirQualityReadingModelValidator _sut = new(new FakeTimeProvider(Now));

    [Theory]
    [InlineData(AirQualityLimits.MinCo2, AirQualityLimits.MinPm25, AirQualityLimits.MinHumidity)]
    [InlineData(803, 25, 36)]
    [InlineData(AirQualityLimits.MaxCo2, AirQualityLimits.MaxPm25, AirQualityLimits.MaxHumidity)]
    public void Validate_ValuesWithinBounds_HasNoErrors(int co2, int pm25, int humidity)
    {
        // Act
        var result = _sut.TestValidate(ValidModel with { Co2 = co2, Pm25 = pm25, Humidity = humidity });

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData(AirQualityLimits.MinCo2 - 1)]
    [InlineData(AirQualityLimits.MaxCo2 + 1)]
    public void Validate_Co2OutOfBounds_HasError(int co2)
    {
        // Act
        var result = _sut.TestValidate(ValidModel with { Co2 = co2 });

        // Assert
        result.ShouldHaveValidationErrorFor(m => m.Co2).Only();
    }

    [Theory]
    [InlineData(AirQualityLimits.MinPm25 - 1)]
    [InlineData(AirQualityLimits.MaxPm25 + 1)]
    public void Validate_Pm25OutOfBounds_HasError(int pm25)
    {
        // Act
        var result = _sut.TestValidate(ValidModel with { Pm25 = pm25 });

        // Assert
        result.ShouldHaveValidationErrorFor(m => m.Pm25).Only();
    }

    [Theory]
    [InlineData(AirQualityLimits.MinHumidity - 1)]
    [InlineData(AirQualityLimits.MaxHumidity + 1)]
    public void Validate_HumidityOutOfBounds_HasError(int humidity)
    {
        // Act
        var result = _sut.TestValidate(ValidModel with { Humidity = humidity });

        // Assert
        result.ShouldHaveValidationErrorFor(m => m.Humidity).Only();
    }

    [Fact]
    public void Validate_InvalidSharedField_HasError()
    {
        // Act
        var result = _sut.TestValidate(ValidModel with { ReceivedAt = default });

        // Assert
        result.ShouldHaveValidationErrorFor(m => m.ReceivedAt).Only();
    }
}
