using DataIngestor.Background.Configuration;
using DataIngestor.UnitTests.Utilities;
using Shouldly;
using Xunit;

namespace DataIngestor.UnitTests.Background.Configuration;

public sealed class MeterPollingWorkerSettingsTests
{
    [Theory]
    [InlineData(1, null)]
    [InlineData(3600, null)]
    [InlineData(5, 1)]
    [InlineData(5, 300)]
    public void Validate_ValuesWithinLimits_Passes(int pollingPeriodInSeconds, int? startupDelayInSeconds)
    {
        // Arrange
        var settings = new MeterPollingWorkerSettings
        {
            PollingPeriodInSeconds = pollingPeriodInSeconds,
            StartupDelayInSeconds = startupDelayInSeconds
        };

        // Act
        var results = DataAnnotationsValidator.Validate(settings);

        // Assert
        results.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(3601)]
    public void Validate_PollingPeriodOutsideOneSecondToOneHour_Fails(int pollingPeriodInSeconds)
    {
        // Arrange
        var settings = new MeterPollingWorkerSettings { PollingPeriodInSeconds = pollingPeriodInSeconds };

        // Act
        var results = DataAnnotationsValidator.Validate(settings);

        // Assert
        results.ShouldHaveSingleItem().MemberNames.ShouldBe([nameof(MeterPollingWorkerSettings.PollingPeriodInSeconds)]);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(301)]
    public void Validate_StartupDelayOutsideOneSecondToFiveMinutes_Fails(int startupDelayInSeconds)
    {
        // Arrange
        var settings = new MeterPollingWorkerSettings
        {
            PollingPeriodInSeconds = 5,
            StartupDelayInSeconds = startupDelayInSeconds
        };

        // Act
        var results = DataAnnotationsValidator.Validate(settings);

        // Assert
        results.ShouldHaveSingleItem().MemberNames.ShouldBe([nameof(MeterPollingWorkerSettings.StartupDelayInSeconds)]);
    }
}
