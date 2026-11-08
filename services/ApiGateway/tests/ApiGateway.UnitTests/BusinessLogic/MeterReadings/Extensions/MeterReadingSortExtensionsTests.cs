using ApiGateway.BusinessLogic.MeterReadings.Extensions;
using ApiGateway.BusinessLogic.MeterReadings.Models;
using ApiGateway.BusinessLogic.MeterReadings.Requests;
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

// The service tests check the resulting order in memory; here the real Npgsql provider translates the sorts
// (no database needed), which shows they run in SQL and don't fail translation.
public sealed class MeterReadingSortExtensionsTests : IAsyncDisposable
{
    private static readonly DateTimeOffset Start = new(2026, 11, 4, 12, 0, 0, TimeSpan.Zero);

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

    [Fact]
    public void ApplySort_GetEnergyStatisticsRequest_OrdersGroupsByLocationThenBucketStartInSql()
    {
        // Arrange
        var request = new GetEnergyStatisticsRequest { From = Start, To = Start.AddHours(1) };

        var statistics = GroupByLocationAndHour().Select(g => new EnergyStatisticsModel
        {
            Location = g.Key.Location,
            BucketStart = g.Key.BucketStart,
            Samples = g.Count(),
            Energy = new MetricStatisticsModel
            {
                Average = g.Average(r => r.Energy!.Value),
                Minimum = g.Min(r => r.Energy!.Value),
                Maximum = g.Max(r => r.Energy!.Value)
            }
        });

        // Act
        var sql = statistics.ApplySort(request).ToQueryString();

        // Assert
        sql.ShouldEndWith("ORDER BY m0.location, m0.\"BucketStart\"");
    }

    [Fact]
    public void ApplySort_GetAirQualityStatisticsRequest_OrdersGroupsByLocationThenBucketStartInSql()
    {
        // Arrange
        var request = new GetAirQualityStatisticsRequest { From = Start, To = Start.AddHours(1) };

        var statistics = GroupByLocationAndHour().Select(g => new AirQualityStatisticsModel
        {
            Location = g.Key.Location,
            BucketStart = g.Key.BucketStart,
            Samples = g.Count(),
            Co2 = new MetricStatisticsModel
            {
                Average = g.Average(r => r.Co2!.Value),
                Minimum = g.Min(r => r.Co2!.Value),
                Maximum = g.Max(r => r.Co2!.Value)
            },
            Pm25 = new MetricStatisticsModel
            {
                Average = g.Average(r => r.Pm25!.Value),
                Minimum = g.Min(r => r.Pm25!.Value),
                Maximum = g.Max(r => r.Pm25!.Value)
            },
            Humidity = new MetricStatisticsModel
            {
                Average = g.Average(r => r.Humidity!.Value),
                Minimum = g.Min(r => r.Humidity!.Value),
                Maximum = g.Max(r => r.Humidity!.Value)
            }
        });

        // Act
        var sql = statistics.ApplySort(request).ToQueryString();

        // Assert
        sql.ShouldEndWith("ORDER BY m0.location, m0.\"BucketStart\"");
    }

    [Fact]
    public void ApplySort_GetMotionStatisticsRequest_OrdersGroupsByLocationThenBucketStartInSql()
    {
        // Arrange
        var request = new GetMotionStatisticsRequest { From = Start, To = Start.AddHours(1) };

        var statistics = GroupByLocationAndHour().Select(g => new MotionStatisticsModel
        {
            Location = g.Key.Location,
            BucketStart = g.Key.BucketStart,
            Samples = g.Count(),
            MotionSamples = g.Count(r => r.MotionDetected == true)
        });

        // Act
        var sql = statistics.ApplySort(request).ToQueryString();

        // Assert
        sql.ShouldEndWith("ORDER BY m0.location, m0.\"BucketStart\"");
    }

    private IQueryable<IGrouping<StatisticsGroupKey, MeterReading>> GroupByLocationAndHour()
    {
        return _dbContext.Query<MeterReading>().GroupByStatisticsKey([StatisticsGrouping.Location, StatisticsGrouping.Time], TimeBucket.Hour);
    }
}
