using ApiGateway.BusinessLogic.MeterReadings.Filtering;
using ApiGateway.BusinessLogic.MeterReadings.Models;
using ApiGateway.BusinessLogic.MeterReadings.Requests;
using ApiGateway.BusinessLogic.MeterReadings.Sorting;
using ApiGateway.Common.Constants.Enums;
using ApiGateway.DataAccess;
using ApiGateway.DataAccess.Database;
using ApiGateway.DataAccess.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace ApiGateway.UnitTests.BusinessLogic.MeterReadings.Sorting;

// The service tests check the resulting order in memory; here the real Npgsql provider translates the sorts
// (no database needed), which shows they run in SQL and don't fail translation.
public sealed class MeterReadingSortExtensionsTests : IAsyncDisposable
{
    private readonly ServiceProvider _serviceProvider;
    private readonly AsyncServiceScope _scope;
    private readonly IReadOnlyDbContext _dbContext;

    public MeterReadingSortExtensionsTests()
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
    public void ApplySort_GetAllReadingsRequest_OrdersByReceivedAtThenIdDescending()
    {
        // Act
        var sql = _dbContext.Query<MeterReading>().ApplySort(new GetAllReadingsRequest()).ToQueryString();

        // Assert
        sql.ShouldEndWith("ORDER BY m.received_at DESC, m.id DESC");
    }

    [Fact]
    public void ApplySort_GetLatestReadingsRequest_OrdersTheWholeUnionByTypeThenLocation()
    {
        // Arrange
        var request = new GetLatestReadingsRequest();

        // Act
        var sql = _dbContext.Query<MeterReading>().ApplyFilter(request).ApplySort(request).ToQueryString();

        // Assert
        sql.ShouldContain("UNION ALL");
        sql.ShouldMatch(@"ORDER BY \w+\.type, \w+\.location$");
    }

    [Theory]
    [InlineData(new[] { ReadingGrouping.Type, ReadingGrouping.Location })]
    [InlineData(new[] { ReadingGrouping.Type })]
    [InlineData(new[] { ReadingGrouping.Location })]
    public void ApplySort_GetReadingCountsRequest_OrdersGroupsInSql(ReadingGrouping[] groupBy)
    {
        // Arrange
        var request = new GetReadingCountsRequest { GroupBy = groupBy };
        var readings = _dbContext.Query<MeterReading>();

        var counts = groupBy.Length == 2
            ? readings.GroupBy(r => new { r.Type, r.Location })
                .Select(g => new ReadingCountModel { Type = g.Key.Type, Location = g.Key.Location, Count = g.Count() })
            : groupBy[0] == ReadingGrouping.Type
                ? readings.GroupBy(r => r.Type).Select(g => new ReadingCountModel { Type = g.Key, Location = null, Count = g.Count() })
                : readings.GroupBy(r => r.Location).Select(g => new ReadingCountModel { Type = null, Location = g.Key, Count = g.Count() });

        // Act
        var sql = counts.ApplySort(request).ToQueryString();

        // Assert
        sql.ShouldContain("GROUP BY");
        sql.ShouldContain("ORDER BY");
    }
}
