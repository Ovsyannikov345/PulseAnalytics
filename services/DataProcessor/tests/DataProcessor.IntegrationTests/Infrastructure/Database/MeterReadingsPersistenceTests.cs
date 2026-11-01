using System.Text.Json;
using Ausiannikau.Utilities.Results;
using DataProcessor.Application.Interfaces.Database;
using DataProcessor.Application.MeterReadings;
using DataProcessor.Application.MeterReadings.Models;
using DataProcessor.Domain.Constants.Enums;
using DataProcessor.Domain.Entities.MeterReadings;
using DataProcessor.Domain.Entities.MeterReadings.Payloads;
using DataProcessor.IntegrationTests.Fixtures;
using DataProcessor.IntegrationTests.Utilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Shouldly;
using Xunit;

namespace DataProcessor.IntegrationTests.Infrastructure.Database;

/// <summary>
/// Stores readings through the processor's own services against the migrated database.
/// </summary>
public sealed class MeterReadingsPersistenceTests(RabbitMqContainerFixture rabbitMq, PostgreSqlContainerFixture postgreSql)
    : IAsyncDisposable
{
    private readonly ProcessorApplicationFactory _factory = new(rabbitMq, postgreSql);

    [Fact]
    public async Task MigrateDatabaseAsync_HostStarted_CreatesUniqueIndexOnDeduplicationKey()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;

        // Act
        _factory.StartServer();

        // Assert
        await using var connection = await postgreSql.CreateConnectionAsync(ct);
        await using var command = new NpgsqlCommand(
            "SELECT indexdef FROM pg_indexes WHERE tablename = 'meter_readings' AND indexname = 'ix_meter_readings_deduplication_key'",
            connection);

        var definition = await command.ExecuteScalarAsync(ct);

        definition.ShouldBeOfType<string>().ShouldStartWith("CREATE UNIQUE INDEX");
    }

    [Fact]
    public async Task ProcessAsync_NewReading_StoresPayloadAsJsonbWithTypeDiscriminator()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var reading = AirQualityReading();

        // Act
        var result = await ProcessAsync(reading, ct);

        // Assert
        result.IsSuccess.ShouldBeTrue();

        await using var connection = await postgreSql.CreateConnectionAsync(ct);
        await using var command = new NpgsqlCommand("SELECT payload::text FROM meter_readings WHERE deduplication_key = @key", connection);
        command.Parameters.AddWithValue("key", reading.DeduplicationKey);

        using var payload = JsonDocument.Parse((await command.ExecuteScalarAsync(ct)).ShouldBeOfType<string>());
        payload.RootElement.GetProperty("$type").GetString().ShouldBe("air_quality");
        payload.RootElement.GetProperty("co2").GetInt32().ShouldBe(803);
        payload.RootElement.GetProperty("pm25").GetInt32().ShouldBe(25);
        payload.RootElement.GetProperty("humidity").GetInt32().ShouldBe(36);
    }

    [Fact]
    public async Task ProcessAsync_ReadingAlreadyStored_SucceedsWithoutAddingRow()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var reading = AirQualityReading();
        await ProcessAsync(reading, ct);

        // Act
        var result = await ProcessAsync(reading, ct);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        (await StoredReadings.GetAsync(_factory, [reading.DeduplicationKey], ct)).ShouldHaveSingleItem();
    }

    [Fact]
    public async Task SaveChangesAsync_DuplicateDeduplicationKey_ThrowsUniqueViolation()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var deduplicationKey = Guid.CreateVersion7();
        await AddAsync(MotionReading(deduplicationKey), ct);

        // Act
        var exception = await Should.ThrowAsync<DbUpdateException>(() => AddAsync(MotionReading(deduplicationKey), ct));

        // Assert
        exception.InnerException.ShouldBeOfType<PostgresException>().SqlState.ShouldBe(PostgresErrorCodes.UniqueViolation);
    }

    public async ValueTask DisposeAsync()
    {
        await _factory.DisposeAsync();
    }

    private static AirQualityReadingModel AirQualityReading()
    {
        return new AirQualityReadingModel
        {
            DeduplicationKey = Guid.CreateVersion7(),
            Location = MeterLocation.Kitchen,
            ReceivedAt = MeterReadingEvents.ReceivedAt,
            Co2 = 803,
            Pm25 = 25,
            Humidity = 36
        };
    }

    private static MeterReading MotionReading(Guid deduplicationKey)
    {
        return new MeterReading
        {
            DeduplicationKey = deduplicationKey,
            Type = MeterType.Motion,
            Location = MeterLocation.Garage,
            ReceivedAt = MeterReadingEvents.ReceivedAt,
            Payload = new MotionPayload { MotionDetected = true }
        };
    }

    private async Task<Result> ProcessAsync(MeterReadingModel reading, CancellationToken ct)
    {
        await using var scope = _factory.Services.CreateAsyncScope();

        var processingService = scope.ServiceProvider.GetRequiredService<IReadingsProcessingService>();

        return await processingService.ProcessAsync(reading, ct);
    }

    private async Task AddAsync(MeterReading reading, CancellationToken ct)
    {
        await using var scope = _factory.Services.CreateAsyncScope();

        var dbContext = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();

        dbContext.Add(reading);
        await dbContext.SaveChangesAsync(ct);
    }
}
