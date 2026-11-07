using System.Buffers.Binary;
using System.Buffers.Text;
using ApiGateway.BusinessLogic.Paging;
using Shouldly;
using Xunit;

namespace ApiGateway.UnitTests.BusinessLogic.Paging;

public sealed class ReadingCursorTests
{
    private const string InvalidCursorMessage = "'Cursor' is invalid.";

    private static readonly DateTimeOffset ReceivedAt = new(2026, 11, 4, 12, 30, 15, 123, TimeSpan.Zero);
    private static readonly Guid Id = Guid.Parse("0199f5c4-3a1e-7b52-9d0e-5c8a7e6b4f21");

    [Fact]
    public void Decode_EncodedCursor_ReturnsSamePosition()
    {
        // Arrange
        var cursor = new ReadingCursor(ReceivedAt.AddTicks(7), Id);

        // Act
        var result = ReadingCursor.Decode(cursor.Encode());

        // Assert
        result.Value.ShouldBe(cursor);
    }

    [Fact]
    public void Constructor_NonUtcReceivedAt_NormalizesToUtc()
    {
        // Act
        var cursor = new ReadingCursor(ReceivedAt.ToOffset(TimeSpan.FromHours(3)), Id);

        // Assert
        cursor.ReceivedAt.Offset.ShouldBe(TimeSpan.Zero);
        cursor.ReceivedAt.ShouldBe(ReceivedAt);
    }

    [Fact]
    public void Encode_DifferentPositions_HaveSameLength()
    {
        // Act
        var earliest = new ReadingCursor(DateTimeOffset.MinValue, Guid.Empty).Encode();
        var latest = new ReadingCursor(DateTimeOffset.MaxValue, Guid.AllBitsSet).Encode();

        // Assert
        earliest.Length.ShouldBe(latest.Length);
    }

    [Fact]
    public void Encode_Always_IsUrlSafe()
    {
        // Act
        var encoded = new ReadingCursor(DateTimeOffset.MaxValue, Guid.AllBitsSet).Encode();

        // Assert
        encoded.ShouldNotContain('+');
        encoded.ShouldNotContain('/');
        encoded.ShouldNotContain('=');
    }

    [Theory]
    [InlineData("")]
    [InlineData("not a cursor")]
    [InlineData("AAAA")]
    public void Decode_MalformedValue_Fails(string value)
    {
        // Act
        var result = ReadingCursor.Decode(value);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.ErrorMessage.ShouldBe(InvalidCursorMessage);
    }

    [Fact]
    public void Decode_ValueLongerThanCursor_Fails()
    {
        // Arrange
        var value = new ReadingCursor(ReceivedAt, Id).Encode() + "AAAA";

        // Act
        var result = ReadingCursor.Decode(value);

        // Assert
        result.IsFailure.ShouldBeTrue();
    }

    [Fact]
    public void Decode_TicksOutOfRange_Fails()
    {
        // Arrange
        var bytes = new byte[24];
        BinaryPrimitives.WriteInt64BigEndian(bytes, long.MaxValue);

        // Act
        var result = ReadingCursor.Decode(Base64Url.EncodeToString(bytes));

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.ErrorMessage.ShouldBe(InvalidCursorMessage);
    }
}
