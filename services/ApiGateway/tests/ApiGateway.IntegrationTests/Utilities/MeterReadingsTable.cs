using System.Text.Json.Nodes;
using ApiGateway.Common.Constants.Enums;
using ApiGateway.IntegrationTests.Fixtures;
using Npgsql;
using NpgsqlTypes;

namespace ApiGateway.IntegrationTests.Utilities;

/// <summary>
/// Writes to the processor's <c>public.meter_readings</c> table, which the gateway reads through the
/// <c>reporting.meter_readings</c> view. The only test code that knows the processor's write format.
/// </summary>
internal static class MeterReadingsTable
{
    public static async Task ClearAsync(PostgreSqlContainerFixture postgreSql, CancellationToken ct)
    {
        await using var connection = await postgreSql.CreateConnectionAsync(ct);
        await using var command = new NpgsqlCommand("TRUNCATE public.meter_readings", connection);

        await command.ExecuteNonQueryAsync(ct);
    }

    public static async Task InsertAsync(
        PostgreSqlContainerFixture postgreSql,
        IEnumerable<SeedReading> readings,
        CancellationToken ct)
    {
        await using var connection = await postgreSql.CreateConnectionAsync(ct);

        foreach (var reading in readings)
        {
            await using var command = new NpgsqlCommand(
                """
                INSERT INTO public.meter_readings (id, deduplication_key, type, location, received_at, payload)
                VALUES (@id, @deduplicationKey, @type, @location, @receivedAt, @payload)
                """,
                connection);

            command.Parameters.AddWithValue("id", reading.Id);
            command.Parameters.AddWithValue("deduplicationKey", Guid.NewGuid());
            command.Parameters.AddWithValue("type", (int)reading.Type);
            command.Parameters.AddWithValue("location", (int)reading.Location);
            // Npgsql writes only UTC values to timestamptz.
            command.Parameters.AddWithValue("receivedAt", reading.ReceivedAt.ToUniversalTime());
            command.Parameters.AddWithValue("payload", NpgsqlDbType.Jsonb, ToPayloadJson(reading));

            await command.ExecuteNonQueryAsync(ct);
        }
    }

    /// <summary>
    /// The processor's polymorphic payload, e.g. <c>{"$type":"energy","energy":845.62}</c>.
    /// </summary>
    private static string ToPayloadJson(SeedReading reading)
    {
        var payload = reading.Type switch
        {
            MeterType.Motion => new JsonObject
            {
                ["$type"] = "motion",
                ["motionDetected"] = reading.MotionDetected
            },
            MeterType.Energy => new JsonObject
            {
                ["$type"] = "energy",
                ["energy"] = reading.Energy
            },
            MeterType.AirQuality => new JsonObject
            {
                ["$type"] = "air_quality",
                ["co2"] = reading.Co2,
                ["pm25"] = reading.Pm25,
                ["humidity"] = reading.Humidity
            },
            _ => throw new ArgumentOutOfRangeException(nameof(reading), reading.Type, "Unknown meter type.")
        };

        return payload.ToJsonString();
    }
}
