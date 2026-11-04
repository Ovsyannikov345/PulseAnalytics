using DataProcessor.Application.MeterReadings;
using DataProcessor.Application.MeterReadings.Models;
using DataProcessor.Domain.Constants.Enums;
using DataProcessor.IntegrationTests.Fixtures;
using DataProcessor.IntegrationTests.Utilities;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Shouldly;
using Xunit;

namespace DataProcessor.IntegrationTests.Infrastructure.Database;

/// <summary>
/// Checks the <c>reporting.meter_readings</c> view, the read contract other services (the API gateway) query.
/// </summary>
public sealed class ReportingViewTests(RabbitMqContainerFixture rabbitMq, PostgreSqlContainerFixture postgreSql)
    : IAsyncDisposable
{
    private readonly ProcessorApplicationFactory _factory = new(rabbitMq, postgreSql);

    [Fact]
    public async Task MigrateDatabaseAsync_HostStarted_CreatesViewWithContractColumns()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;

        // Act
        _factory.StartServer();

        // Assert
        await using var connection = await postgreSql.CreateConnectionAsync(ct);
        await using var command = new NpgsqlCommand(
            """
            SELECT column_name, data_type
            FROM information_schema.columns
            WHERE table_schema = 'reporting' AND table_name = 'meter_readings'
            ORDER BY ordinal_position
            """,
            connection);
        await using var reader = await command.ExecuteReaderAsync(ct);

        var columns = new List<(string Name, string DataType)>();

        while (await reader.ReadAsync(ct))
        {
            columns.Add((reader.GetString(0), reader.GetString(1)));
        }

        columns.ShouldBe(
        [
            ("id", "uuid"),
            ("type", "integer"),
            ("location", "integer"),
            ("received_at", "timestamp with time zone"),
            ("motion_detected", "boolean"),
            ("energy", "double precision"),
            ("co2", "integer"),
            ("pm25", "integer"),
            ("humidity", "integer")
        ]);
    }

    [Fact]
    public async Task ProcessAsync_MotionReading_IsReadableFromViewWithMotionColumnOnly()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var reading = new MotionReadingModel
        {
            DeduplicationKey = Guid.CreateVersion7(),
            Location = MeterLocation.Garage,
            ReceivedAt = MeterReadingEvents.ReceivedAt,
            MotionDetected = true
        };

        // Act
        var row = await ProcessAndReadFromViewAsync(reading, ct);

        // Assert
        row.ShouldBe(new ReportingViewRow(
            MeterType.Motion,
            MeterLocation.Garage,
            MeterReadingEvents.ReceivedAt,
            MotionDetected: true,
            Energy: null,
            Co2: null,
            Pm25: null,
            Humidity: null));
    }

    [Fact]
    public async Task ProcessAsync_EnergyReading_IsReadableFromViewWithEnergyColumnOnly()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var reading = new EnergyReadingModel
        {
            DeduplicationKey = Guid.CreateVersion7(),
            Location = MeterLocation.Office,
            ReceivedAt = MeterReadingEvents.ReceivedAt,
            Energy = 845.62
        };

        // Act
        var row = await ProcessAndReadFromViewAsync(reading, ct);

        // Assert
        row.ShouldBe(new ReportingViewRow(
            MeterType.Energy,
            MeterLocation.Office,
            MeterReadingEvents.ReceivedAt,
            MotionDetected: null,
            Energy: 845.62,
            Co2: null,
            Pm25: null,
            Humidity: null));
    }

    [Fact]
    public async Task ProcessAsync_AirQualityReading_IsReadableFromViewWithAirQualityColumnsOnly()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var reading = new AirQualityReadingModel
        {
            DeduplicationKey = Guid.CreateVersion7(),
            Location = MeterLocation.LivingRoom,
            ReceivedAt = MeterReadingEvents.ReceivedAt,
            Co2 = 803,
            Pm25 = 25,
            Humidity = 36
        };

        // Act
        var row = await ProcessAndReadFromViewAsync(reading, ct);

        // Assert
        row.ShouldBe(new ReportingViewRow(
            MeterType.AirQuality,
            MeterLocation.LivingRoom,
            MeterReadingEvents.ReceivedAt,
            MotionDetected: null,
            Energy: null,
            Co2: 803,
            Pm25: 25,
            Humidity: 36));
    }

    public async ValueTask DisposeAsync()
    {
        await _factory.DisposeAsync();
    }

    private static T? GetNullable<T>(NpgsqlDataReader reader, string column)
        where T : struct
    {
        var ordinal = reader.GetOrdinal(column);

        return reader.IsDBNull(ordinal) ? null : reader.GetFieldValue<T>(ordinal);
    }

    private async Task<ReportingViewRow> ProcessAndReadFromViewAsync(MeterReadingModel reading, CancellationToken ct)
    {
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var processingService = scope.ServiceProvider.GetRequiredService<IReadingsProcessingService>();

            (await processingService.ProcessAsync(reading, ct)).IsSuccess.ShouldBeTrue();
        }

        // The view has no deduplication key, so the row is looked up by the ID the processor generated.
        var id = (await StoredReadings.GetAsync(_factory, [reading.DeduplicationKey], ct)).ShouldHaveSingleItem().Id;

        await using var connection = await postgreSql.CreateConnectionAsync(ct);
        await using var command = new NpgsqlCommand(
            """
            SELECT type, location, received_at, motion_detected, energy, co2, pm25, humidity
            FROM reporting.meter_readings
            WHERE id = @id
            """,
            connection);
        command.Parameters.AddWithValue("id", id);
        await using var reader = await command.ExecuteReaderAsync(ct);

        (await reader.ReadAsync(ct)).ShouldBeTrue();

        return new ReportingViewRow(
            (MeterType)reader.GetInt32(reader.GetOrdinal("type")),
            (MeterLocation)reader.GetInt32(reader.GetOrdinal("location")),
            reader.GetFieldValue<DateTimeOffset>(reader.GetOrdinal("received_at")),
            GetNullable<bool>(reader, "motion_detected"),
            GetNullable<double>(reader, "energy"),
            GetNullable<int>(reader, "co2"),
            GetNullable<int>(reader, "pm25"),
            GetNullable<int>(reader, "humidity"));
    }

    private sealed record ReportingViewRow(
        MeterType Type,
        MeterLocation Location,
        DateTimeOffset ReceivedAt,
        bool? MotionDetected,
        double? Energy,
        int? Co2,
        int? Pm25,
        int? Humidity);
}
