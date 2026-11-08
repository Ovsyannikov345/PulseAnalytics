using ApiGateway.BusinessLogic.MeterReadings.Extensions;
using ApiGateway.BusinessLogic.MeterReadings.Requests;
using ApiGateway.BusinessLogic.Paging;
using ApiGateway.Common.Constants.Enums;
using ApiGateway.DataAccess;
using ApiGateway.DataAccess.Database;
using ApiGateway.DataAccess.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace ApiGateway.UnitTests.BusinessLogic.MeterReadings.Extensions;

// The service tests run these conditions in memory; here the real Npgsql provider translates them (no database needed),
// which shows they become index-friendly SQL instead of failing translation or running on the client.
public sealed class MeterReadingFilterExtensionsTests : IAsyncDisposable
{
    private static readonly DateTimeOffset Start = new(2026, 11, 4, 12, 0, 0, TimeSpan.Zero);

    private readonly ServiceProvider _serviceProvider;
    private readonly AsyncServiceScope _scope;
    private readonly IReadOnlyDbContext _dbContext;

    public MeterReadingFilterExtensionsTests()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Database:ConnectionString"] = "Host=localhost;Database=unused"
            })
            .Build();

        _serviceProvider = new ServiceCollection()
            .AddDataAccess(configuration)
            .BuildServiceProvider();

        _scope = _serviceProvider.CreateAsyncScope();
        _dbContext = _scope.ServiceProvider.GetRequiredService<IReadOnlyDbContext>();
    }

    public async ValueTask DisposeAsync()
    {
        await _scope.DisposeAsync();
        await _serviceProvider.DisposeAsync();
    }

    [Fact]
    public void ApplyFilter_GetAllReadingsRequestWithAllFields_TranslatesToSqlConditions()
    {
        // Arrange
        var request = new GetAllReadingsRequest
        {
            Types = [MeterType.Energy],
            Locations = [MeterLocation.Kitchen, MeterLocation.Office],
            From = Start,
            To = Start.AddHours(1)
        };

        // Act
        var sql = _dbContext.Query<MeterReading>().ApplyFilter(request).ToQueryString();

        // Assert
        sql.ShouldContain("m.type = ANY (@types)");
        sql.ShouldContain("m.location = ANY (@locations)");
        sql.ShouldContain("m.received_at >= @fromUtc");
        sql.ShouldContain("m.received_at < @toUtc");
    }

    [Fact]
    public void ApplyFilter_ExportReadingsRequestWithAllFields_TranslatesToSqlConditions()
    {
        // Arrange
        var request = new ExportReadingsRequest
        {
            Types = [MeterType.Energy],
            Locations = [MeterLocation.Kitchen],
            From = Start,
            To = Start.AddDays(1)
        };

        // Act
        var sql = _dbContext.Query<MeterReading>().ApplyFilter(request).ToQueryString();

        // Assert
        sql.ShouldContain("m.type = ANY (@types)");
        sql.ShouldContain("m.location = ANY (@locations)");
        sql.ShouldContain("m.received_at >= @fromUtc");
        sql.ShouldContain("m.received_at < @toUtc");
    }

    [Fact]
    public void ApplyFilter_ExportReadingsRequestWithRangeOnly_FiltersOnlyByReceivedAt()
    {
        // Act
        var sql = _dbContext.Query<MeterReading>()
            .ApplyFilter(new ExportReadingsRequest { Types = [], From = Start, To = Start.AddDays(1) })
            .ToQueryString();

        // Assert
        sql.ShouldContain("m.received_at >= @fromUtc");
        sql.ShouldContain("m.received_at < @toUtc");
        sql.ShouldNotContain("ANY");
    }

    [Fact]
    public void ApplyFilter_EmptyGetAllReadingsRequest_AddsNoConditions()
    {
        // Act
        var sql = _dbContext.Query<MeterReading>().ApplyFilter(new GetAllReadingsRequest { Types = [] }).ToQueryString();

        // Assert
        sql.ShouldNotContain("WHERE");
    }

    [Fact]
    public void ApplyFilter_GetReadingCountsRequestWithAllFields_TranslatesToSqlConditions()
    {
        // Arrange
        var request = new GetReadingCountsRequest
        {
            Types = [MeterType.Motion],
            Locations = [MeterLocation.Garage],
            From = Start,
            To = Start.AddHours(1)
        };

        // Act
        var sql = _dbContext.Query<MeterReading>().ApplyFilter(request).ToQueryString();

        // Assert
        sql.ShouldContain("m.type = ANY (@types)");
        sql.ShouldContain("m.location = ANY (@locations)");
        sql.ShouldContain("m.received_at >= @fromUtc");
        sql.ShouldContain("m.received_at < @toUtc");
    }

    [Fact]
    public void ApplyFilter_GetLatestReadingsRequest_TranslatesToOneLimitedLookupPerMeter()
    {
        // Arrange
        var request = new GetLatestReadingsRequest
        {
            Types = [MeterType.Energy, MeterType.Motion],
            Locations = [MeterLocation.Kitchen, MeterLocation.Office]
        };

        // Act
        var sql = _dbContext.Query<MeterReading>().ApplyFilter(request).ToQueryString();

        // Assert
        // 2 types x 2 locations = 4 lookups joined by 3 UNION ALLs.
        sql.Split("UNION ALL").Length.ShouldBe(4);
        sql.Split("LIMIT ").Length.ShouldBe(5);
    }

    [Fact]
    public void ApplyFilter_GetEnergyStatisticsRequest_TranslatesToEnergyTypeAndSqlConditions()
    {
        // Arrange
        var request = new GetEnergyStatisticsRequest
        {
            Locations = [MeterLocation.Kitchen],
            From = Start,
            To = Start.AddHours(1)
        };

        // Act
        var sql = _dbContext.Query<MeterReading>().ApplyFilter(request).ToQueryString();

        // Assert
        // A constant type keeps the (type, location, received_at) index usable.
        sql.ShouldContain("m.type = 2");
        sql.ShouldContain("m.location = ANY (@locations)");
        sql.ShouldContain("m.received_at >= @fromUtc");
        sql.ShouldContain("m.received_at < @toUtc");
    }

    [Fact]
    public void ApplyFilter_GetAirQualityStatisticsRequest_TranslatesToAirQualityTypeAndSqlConditions()
    {
        // Arrange
        var request = new GetAirQualityStatisticsRequest
        {
            Locations = [MeterLocation.Office],
            From = Start,
            To = Start.AddHours(1)
        };

        // Act
        var sql = _dbContext.Query<MeterReading>().ApplyFilter(request).ToQueryString();

        // Assert
        sql.ShouldContain("m.type = 1");
        sql.ShouldContain("m.location = ANY (@locations)");
        sql.ShouldContain("m.received_at >= @fromUtc");
        sql.ShouldContain("m.received_at < @toUtc");
    }

    [Fact]
    public void ApplyFilter_GetMotionStatisticsRequestWithoutLocations_TranslatesToMotionTypeAndTimeRange()
    {
        // Arrange
        var request = new GetMotionStatisticsRequest
        {
            From = Start,
            To = Start.AddHours(1)
        };

        // Act
        var sql = _dbContext.Query<MeterReading>().ApplyFilter(request).ToQueryString();

        // Assert
        sql.ShouldContain("m.type = 3");
        sql.ShouldNotContain("m.location = ANY");
        sql.ShouldContain("m.received_at >= @fromUtc");
        sql.ShouldContain("m.received_at < @toUtc");
    }

    [Fact]
    public void ApplyCursorPagination_Cursor_TranslatesToRangeOnReceivedAtAndIdTieBreak()
    {
        // Arrange
        var cursor = new ReadingCursor(Start, Guid.CreateVersion7());

        // Act
        var sql = _dbContext.Query<MeterReading>().ApplyCursorPagination(cursor).ToQueryString();

        // Assert
        sql.ShouldContain("m.received_at <= @receivedAt");
        sql.ShouldContain("m.received_at < @receivedAt");
        sql.ShouldContain("m.id < @id");
    }
}
