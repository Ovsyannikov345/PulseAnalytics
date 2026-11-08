using ApiGateway.BusinessLogic.MeterReadings.Extensions;
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

// The service tests check the groups in memory; here the real Npgsql provider translates the grouping (no database needed),
// which shows it runs in SQL, buckets with date_bin on the UTC grid and always has a GROUP BY.
public sealed class StatisticsGroupingExtensionsTests : IAsyncDisposable
{
    private const string HourBucketSql = "date_bin(@bucketLength, m.received_at, TIMESTAMPTZ '1970-01-01T00:00:00+00:00')";

    private readonly ServiceProvider _serviceProvider;
    private readonly AsyncServiceScope _scope;
    private readonly IReadOnlyDbContext _dbContext;

    public StatisticsGroupingExtensionsTests()
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
    public void GroupByStatisticsKey_LocationAndTime_GroupsByLocationAndDateBinInSql()
    {
        // Act
        var sql = ToCountsSql([StatisticsGrouping.Location, StatisticsGrouping.Time], TimeBucket.Hour);

        // Assert
        sql.ShouldContain("-- @bucketLength='01:00:00'");
        sql.ShouldContain($"m.location, {HourBucketSql} AS \"BucketStart\"");
        sql.ShouldContain("GROUP BY m0.location, m0.\"BucketStart\"");
    }

    [Fact]
    public void GroupByStatisticsKey_Time_GroupsByDateBinInSql()
    {
        // Act
        var sql = ToCountsSql([StatisticsGrouping.Time], TimeBucket.Hour);

        // Assert
        sql.ShouldContain($"NULL AS \"Location0\", {HourBucketSql} AS \"BucketStart\"");
        sql.ShouldContain("GROUP BY m0.\"Location0\", m0.\"BucketStart\"");
    }

    [Fact]
    public void GroupByStatisticsKey_Location_GroupsByLocationWithoutDateBin()
    {
        // Act
        var sql = ToCountsSql([StatisticsGrouping.Location], bucket: null);

        // Assert
        sql.ShouldContain("GROUP BY m0.location, m0.\"BucketStart\"");
        sql.ShouldNotContain("date_bin");
    }

    // A GROUP BY makes an empty range return no rows; a plain aggregate would return one row of NULLs.
    [Fact]
    public void GroupByStatisticsKey_NoGrouping_StillHasGroupBy()
    {
        // Act
        var sql = ToCountsSql([], bucket: null);

        // Assert
        sql.ShouldContain("GROUP BY m0.\"Location0\", m0.\"BucketStart\"");
        sql.ShouldNotContain("date_bin");
    }

    [Fact]
    public void GroupByStatisticsKey_TimeWithoutBucket_Throws()
    {
        // Act & Assert
        Should.Throw<ArgumentException>(() => _dbContext.Query<MeterReading>().GroupByStatisticsKey([StatisticsGrouping.Time], bucket: null));
    }

    private string ToCountsSql(StatisticsGrouping[] groupBy, TimeBucket? bucket)
    {
        return _dbContext.Query<MeterReading>()
            .GroupByStatisticsKey(groupBy, bucket)
            .Select(g => new { g.Key.Location, g.Key.BucketStart, Count = g.Count() })
            .ToQueryString();
    }
}
