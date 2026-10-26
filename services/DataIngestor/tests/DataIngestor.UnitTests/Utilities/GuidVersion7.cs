using System.Buffers.Binary;

namespace DataIngestor.UnitTests.Utilities;

/// <summary>
/// Reads the parts of a version 7 GUID that tests assert on.
/// </summary>
internal static class GuidVersion7
{
    /// <summary>
    /// Returns the Unix timestamp in milliseconds stored in the first 48 bits of the GUID.
    /// </summary>
    public static DateTimeOffset GetTimestamp(Guid guid)
    {
        Span<byte> bytes = stackalloc byte[16];
        guid.TryWriteBytes(bytes, bigEndian: true, out _);

        var unixTimeMilliseconds = (long)(BinaryPrimitives.ReadUInt64BigEndian(bytes) >> 16);

        return DateTimeOffset.FromUnixTimeMilliseconds(unixTimeMilliseconds);
    }
}
