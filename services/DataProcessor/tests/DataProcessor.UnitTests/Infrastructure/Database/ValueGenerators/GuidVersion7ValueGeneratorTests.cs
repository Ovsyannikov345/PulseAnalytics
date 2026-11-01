using DataProcessor.Infrastructure.Database.ValueGenerators;
using Shouldly;
using Xunit;

namespace DataProcessor.UnitTests.Infrastructure.Database.ValueGenerators;

public sealed class GuidVersion7ValueGeneratorTests
{
    private readonly GuidVersion7ValueGenerator _sut = new();

    [Fact]
    public void Next_Called_ReturnsUuidVersion7()
    {
        // Act
        var value = _sut.Next(null!);

        // Assert
        value.Version.ShouldBe(7);
    }

    [Fact]
    public void Next_CalledRepeatedly_ReturnsDifferentValues()
    {
        // Act
        var values = Enumerable.Range(0, 100).Select(_ => _sut.Next(null!)).ToList();

        // Assert
        values.Distinct().Count().ShouldBe(values.Count);
    }

    [Fact]
    public void GeneratesTemporaryValues_Always_IsFalse()
    {
        // Act & Assert
        _sut.GeneratesTemporaryValues.ShouldBeFalse();
    }
}
