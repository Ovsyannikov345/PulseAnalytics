using System.Buffers;
using System.Buffers.Binary;
using System.Buffers.Text;
using Ausiannikau.Utilities.Results;

namespace ApiGateway.BusinessLogic.Paging;

/// <summary>
/// Position of a reading in the <c>(ReceivedAt DESC, Id DESC)</c> order used by keyset paging.
/// Encoded as base64url of the big-endian UTC ticks followed by the id bytes, so every cursor is opaque and has the same length.
/// </summary>
internal readonly record struct ReadingCursor
{
    private const int TicksLength = sizeof(long);

    private const int IdLength = 16;

    private const int EncodedLength = TicksLength + IdLength;

    private const string InvalidCursorMessage = "'Cursor' is invalid.";

    public DateTimeOffset ReceivedAt { get; }

    public Guid Id { get; }

    public ReadingCursor(DateTimeOffset receivedAt, Guid id)
    {
        ReceivedAt = receivedAt.ToUniversalTime();
        Id = id;
    }

    public static Result<ReadingCursor> Decode(string cursor)
    {
        Span<byte> bytes = stackalloc byte[EncodedLength];

        var status = Base64Url.DecodeFromChars(cursor, bytes, out _, out var bytesWritten);

        if (status != OperationStatus.Done || bytesWritten != EncodedLength)
        {
            return Result<ReadingCursor>.Fail(InvalidCursorMessage);
        }

        var ticks = BinaryPrimitives.ReadInt64BigEndian(bytes);

        if (ticks < DateTimeOffset.MinValue.UtcTicks || ticks > DateTimeOffset.MaxValue.UtcTicks)
        {
            return Result<ReadingCursor>.Fail(InvalidCursorMessage);
        }

        var receivedAt = new DateTimeOffset(ticks, TimeSpan.Zero);

        var id = new Guid(bytes[TicksLength..], bigEndian: true);

        return Result<ReadingCursor>.Succeed(new ReadingCursor(receivedAt, id));
    }

    public string Encode()
    {
        Span<byte> bytes = stackalloc byte[EncodedLength];

        BinaryPrimitives.WriteInt64BigEndian(bytes, ReceivedAt.UtcTicks);

        Id.ToByteArray(bigEndian: true).CopyTo(bytes[TicksLength..]);

        return Base64Url.EncodeToString(bytes);
    }
}
