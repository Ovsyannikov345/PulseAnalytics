using System.Buffers;
using System.Collections.Frozen;
using System.Globalization;
using System.Text;
using ApiGateway.BusinessLogic.MeterReadings.Models;
using ApiGateway.Common.Constants.Enums;

namespace ApiGateway.Api.Rest.Csv;

/// <summary>
/// Writes readings as UTF-8 CSV (RFC 4180: comma-separated, CRLF line endings, header row first).
/// </summary>
internal static class ReadingsCsvWriter
{
    private const int MaxValueLength = 64;

    private static readonly FrozenDictionary<MeterType, byte[]> TypeNames = EncodeNames<MeterType>();

    private static readonly FrozenDictionary<MeterLocation, byte[]> LocationNames = EncodeNames<MeterLocation>();

    private static ReadOnlySpan<byte> Header => "id,type,location,received_at,motion_detected,energy,co2,pm25,humidity\r\n"u8;

    private static ReadOnlySpan<byte> Separator => ","u8;

    private static ReadOnlySpan<byte> LineEnd => "\r\n"u8;

    private static ReadOnlySpan<byte> True => "true"u8;

    private static ReadOnlySpan<byte> False => "false"u8;

    public static void WriteHeader(IBufferWriter<byte> writer)
    {
        writer.Write(Header);
    }

    public static void WriteRow(IBufferWriter<byte> writer, ExportReadingModel reading)
    {
        WriteValue(writer, reading.Id, "D");
        writer.Write(Separator);
        writer.Write(TypeNames[reading.Type]);
        writer.Write(Separator);
        writer.Write(LocationNames[reading.Location]);
        writer.Write(Separator);
        WriteValue(writer, reading.ReceivedAt.UtcDateTime, "O");
        writer.Write(Separator);

        if (reading.MotionDetected is { } motionDetected)
        {
            writer.Write(motionDetected ? True : False);
        }

        writer.Write(Separator);
        WriteOptionalValue(writer, reading.Energy);
        writer.Write(Separator);
        WriteOptionalValue(writer, reading.Co2);
        writer.Write(Separator);
        WriteOptionalValue(writer, reading.Pm25);
        writer.Write(Separator);
        WriteOptionalValue(writer, reading.Humidity);
        writer.Write(LineEnd);
    }

    private static void WriteOptionalValue<TValue>(IBufferWriter<byte> writer, TValue? value)
        where TValue : struct, IUtf8SpanFormattable
    {
        if (value is { } definedValue)
        {
            WriteValue(writer, definedValue, default);
        }
    }

    private static void WriteValue<TValue>(IBufferWriter<byte> writer, TValue value, ReadOnlySpan<char> format)
        where TValue : IUtf8SpanFormattable
    {
        var span = writer.GetSpan(MaxValueLength);

        if (!value.TryFormat(span, out var bytesWritten, format, CultureInfo.InvariantCulture))
        {
            throw new InvalidOperationException($"The {typeof(TValue).Name} value doesn't fit into {MaxValueLength} bytes.");
        }

        writer.Advance(bytesWritten);
    }

    private static FrozenDictionary<TEnum, byte[]> EncodeNames<TEnum>()
        where TEnum : struct, Enum
    {
        return Enum.GetValues<TEnum>().ToFrozenDictionary(value => value, value => Encoding.UTF8.GetBytes(value.ToString()));
    }
}
