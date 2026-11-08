using ApiGateway.Common.Constants.Enums;
using Shouldly;
using Xunit;

namespace ApiGateway.UnitTests.Common.Constants.Enums;

public sealed class TimeBucketExtensionsTests
{
    [Theory]
    [InlineData(TimeBucket.Minute, 1)]
    [InlineData(TimeBucket.FiveMinutes, 5)]
    [InlineData(TimeBucket.FifteenMinutes, 15)]
    [InlineData(TimeBucket.Hour, 60)]
    [InlineData(TimeBucket.Day, 1440)]
    public void ToTimeSpan_DefinedBucket_ReturnsItsLength(TimeBucket bucket, int expectedMinutes)
    {
        // Act
        var length = bucket.ToTimeSpan();

        // Assert
        length.ShouldBe(TimeSpan.FromMinutes(expectedMinutes));
    }

    [Fact]
    public void ToTimeSpan_UndefinedBucket_Throws()
    {
        // Act & Assert
        Should.Throw<ArgumentOutOfRangeException>(() => ((TimeBucket)99).ToTimeSpan());
    }
}
