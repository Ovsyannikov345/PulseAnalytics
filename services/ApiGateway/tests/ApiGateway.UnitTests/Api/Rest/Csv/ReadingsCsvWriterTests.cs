using System.Buffers;
using System.Globalization;
using System.Text;
using ApiGateway.Api.Rest.Csv;
using ApiGateway.BusinessLogic.MeterReadings.Models;
using ApiGateway.Common.Constants.Enums;
using Shouldly;
using Xunit;

namespace ApiGateway.UnitTests.Api.Rest.Csv;

public sealed class ReadingsCsvWriterTests
{
    private static readonly Guid Id = Guid.Parse("0196a0c4-5b2e-7d10-9f3a-1c2d3e4f5a6b");

    private static readonly DateTimeOffset ReceivedAt = new(2026, 11, 4, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void WriteHeader_WritesColumnNamesAndCrLf()
    {
        // Arrange
        var writer = new ArrayBufferWriter<byte>();

        // Act
        ReadingsCsvWriter.WriteHeader(writer);

        // Assert
        Written(writer).ShouldBe("id,type,location,received_at,motion_detected,energy,co2,pm25,humidity\r\n");
    }

    [Theory]
    [InlineData(true, "true")]
    [InlineData(false, "false")]
    public void WriteRow_MotionReading_WritesOnlyMotionColumn(bool motionDetected, string expectedValue)
    {
        // Act
        var row = WriteRow(Reading(MeterType.Motion, MeterLocation.LivingRoom) with { MotionDetected = motionDetected });

        // Assert
        row.ShouldBe($"{Id},Motion,LivingRoom,2026-11-04T12:00:00.0000000Z,{expectedValue},,,,\r\n");
    }

    [Fact]
    public void WriteRow_EnergyReading_WritesOnlyEnergyColumn()
    {
        // Act
        var row = WriteRow(Reading(MeterType.Energy, MeterLocation.Kitchen) with { Energy = 1.5 });

        // Assert
        row.ShouldBe($"{Id},Energy,Kitchen,2026-11-04T12:00:00.0000000Z,,1.5,,,\r\n");
    }

    [Fact]
    public void WriteRow_AirQualityReading_WritesOnlyAirQualityColumns()
    {
        // Act
        var row = WriteRow(Reading(MeterType.AirQuality, MeterLocation.Office) with { Co2 = 800, Pm25 = 15, Humidity = 40 });

        // Assert
        row.ShouldBe($"{Id},AirQuality,Office,2026-11-04T12:00:00.0000000Z,,,800,15,40\r\n");
    }

    [Fact]
    public void WriteRow_TimestampWithOffset_WritesUtc()
    {
        // Arrange
        var receivedAt = new DateTimeOffset(2026, 11, 4, 14, 30, 15, 123, TimeSpan.FromHours(2));

        // Act
        var row = WriteRow(Reading(MeterType.Energy, MeterLocation.Kitchen) with { ReceivedAt = receivedAt, Energy = 1 });

        // Assert
        row.ShouldContain(",2026-11-04T12:30:15.1230000Z,");
    }

    [Fact]
    public void WriteRow_CultureWithCommaDecimalSeparator_WritesInvariantNumbers()
    {
        // Arrange
        var originalCulture = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");

        try
        {
            // Act
            var row = WriteRow(Reading(MeterType.Energy, MeterLocation.Kitchen) with { Energy = 1234.5 });

            // Assert
            row.ShouldContain(",1234.5,");
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
        }
    }

    [Fact]
    public void WriteRow_SmallDouble_WritesShortestRoundTrippableValue()
    {
        // Act
        var row = WriteRow(Reading(MeterType.Energy, MeterLocation.Kitchen) with { Energy = 0.1 + 0.2 });

        // Assert
        row.ShouldContain(",0.30000000000000004,");
    }

    [Fact]
    public void WriteRow_SeveralRows_AppendsThem()
    {
        // Arrange
        var writer = new ArrayBufferWriter<byte>();

        // Act
        ReadingsCsvWriter.WriteHeader(writer);
        ReadingsCsvWriter.WriteRow(writer, Reading(MeterType.Energy, MeterLocation.Kitchen) with { Energy = 1 });
        ReadingsCsvWriter.WriteRow(writer, Reading(MeterType.Energy, MeterLocation.Garage) with { Energy = 2 });

        // Assert
        Written(writer).Split("\r\n").ShouldBe(
        [
            "id,type,location,received_at,motion_detected,energy,co2,pm25,humidity",
            $"{Id},Energy,Kitchen,2026-11-04T12:00:00.0000000Z,,1,,,",
            $"{Id},Energy,Garage,2026-11-04T12:00:00.0000000Z,,2,,,",
            string.Empty
        ]);
    }

    [Fact]
    public void WriteRow_UndefinedType_Throws()
    {
        // Act & Assert
        Should.Throw<KeyNotFoundException>(() => WriteRow(Reading((MeterType)42, MeterLocation.Kitchen)))
            .Message.ShouldContain("42");
    }

    private static ExportReadingModel Reading(MeterType type, MeterLocation location)
    {
        return new ExportReadingModel
        {
            Id = Id,
            Type = type,
            Location = location,
            ReceivedAt = ReceivedAt
        };
    }

    private static string WriteRow(ExportReadingModel reading)
    {
        var writer = new ArrayBufferWriter<byte>();

        ReadingsCsvWriter.WriteRow(writer, reading);

        return Written(writer);
    }

    private static string Written(ArrayBufferWriter<byte> writer)
    {
        return Encoding.UTF8.GetString(writer.WrittenSpan);
    }
}
