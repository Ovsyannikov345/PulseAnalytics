using ApiGateway.BusinessLogic.MeterReadings.Requests;
using ApiGateway.BusinessLogic.MeterReadings.Validators;
using ApiGateway.Common.Constants.Enums;
using FluentValidation.TestHelper;
using Shouldly;
using Xunit;

namespace ApiGateway.UnitTests.BusinessLogic.MeterReadings.Validators;

public sealed class GetAirQualityStatisticsRequestValidatorTests
{
    private static readonly DateTimeOffset From = new(2026, 11, 4, 10, 0, 0, TimeSpan.Zero);

    private static readonly GetAirQualityStatisticsRequest ValidRequest = new()
    {
        Locations = [MeterLocation.Kitchen, MeterLocation.Office],
        From = From,
        To = From.AddDays(1),
        GroupBy = [StatisticsGrouping.Location, StatisticsGrouping.Time],
        Bucket = TimeBucket.Hour
    };

    private readonly GetAirQualityStatisticsRequestValidator _sut = new();

    [Fact]
    public void Validate_ValidRequest_HasNoErrors()
    {
        // Act
        var result = _sut.TestValidate(ValidRequest);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_NoGroupingAndNoBucket_HasNoErrors()
    {
        // Act
        var result = _sut.TestValidate(ValidRequest with { Locations = null, GroupBy = [], Bucket = null });

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_UndefinedLocationGroupingAndBucket_HasErrors()
    {
        // Act
        var result = _sut.TestValidate(ValidRequest with
        {
            Locations = [(MeterLocation)0],
            GroupBy = [StatisticsGrouping.Time, (StatisticsGrouping)9],
            Bucket = (TimeBucket)99
        });

        // Assert
        result.ShouldHaveValidationErrorFor("Locations[0]");
        result.ShouldHaveValidationErrorFor("GroupBy[1]");
        result.ShouldHaveValidationErrorFor(r => r.Bucket);
    }

    [Fact]
    public void Validate_FromNotEarlierThanTo_HasError()
    {
        // Act
        var result = _sut.TestValidate(ValidRequest with { To = From });

        // Assert
        var error = result.ShouldHaveValidationErrorFor(r => r.From).Only().ShouldHaveSingleItem();
        error.ErrorMessage.ShouldBe("'From' must be earlier than 'To'.");
    }

    [Fact]
    public void Validate_RangeOfMaxLength_HasNoErrors()
    {
        // Act
        var result = _sut.TestValidate(ValidRequest with { To = From.AddDays(31), Bucket = TimeBucket.Day });

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_RangeLongerThanMaxLength_HasError()
    {
        // Act
        var result = _sut.TestValidate(ValidRequest with { To = From.AddDays(31).AddTicks(1), Bucket = TimeBucket.Day });

        // Assert
        var error = result.ShouldHaveValidationErrorFor(r => r.To).Only().ShouldHaveSingleItem();
        error.ErrorMessage.ShouldBe("'To' must be at most 31 days after 'From'.");
    }

    [Fact]
    public void Validate_TimeGroupingWithoutBucket_HasError()
    {
        // Act
        var result = _sut.TestValidate(ValidRequest with { Bucket = null });

        // Assert
        var error = result.ShouldHaveValidationErrorFor(r => r.Bucket).Only().ShouldHaveSingleItem();
        error.ErrorMessage.ShouldBe("'Bucket' is required when grouping by time.");
    }

    [Fact]
    public void Validate_BucketWithoutTimeGrouping_HasError()
    {
        // Act
        var result = _sut.TestValidate(ValidRequest with { GroupBy = [StatisticsGrouping.Location] });

        // Assert
        var error = result.ShouldHaveValidationErrorFor(r => r.Bucket).Only().ShouldHaveSingleItem();
        error.ErrorMessage.ShouldBe("'Bucket' must be empty unless grouping by time.");
    }

    [Fact]
    public void Validate_MaxBucketCount_HasNoErrors()
    {
        // Act
        var result = _sut.TestValidate(ValidRequest with { To = From.AddMinutes(1000), Bucket = TimeBucket.Minute });

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_MoreThanMaxBucketCount_HasError()
    {
        // Act
        var result = _sut.TestValidate(ValidRequest with { To = From.AddMinutes(1000).AddTicks(1), Bucket = TimeBucket.Minute });

        // Assert
        var error = result.ShouldHaveValidationErrorFor(r => r.Bucket).Only().ShouldHaveSingleItem();
        error.ErrorMessage.ShouldBe("'Bucket' must split the range into at most 1000 buckets.");
    }

    [Fact]
    public void Validate_MaxBucketCountNotAlignedWithGrid_HasNoErrors()
    {
        // Act
        // The limit is on the range's length: starting mid-minute touches 1001 minute buckets, which is still allowed.
        var from = From.AddSeconds(30);
        var result = _sut.TestValidate(ValidRequest with { From = from, To = from.AddMinutes(1000), Bucket = TimeBucket.Minute });

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }
}
