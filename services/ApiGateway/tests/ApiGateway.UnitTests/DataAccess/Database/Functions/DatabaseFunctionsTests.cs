using ApiGateway.DataAccess;
using ApiGateway.DataAccess.Database;
using ApiGateway.DataAccess.Database.Functions;
using ApiGateway.DataAccess.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace ApiGateway.UnitTests.DataAccess.Database.Functions;

// The C# body of DateBin must match PostgreSQL's date_bin: the in-memory service tests rely on it.
// The integration tests compare the two against a real database.
public sealed class DatabaseFunctionsTests : IAsyncDisposable
{
    private static readonly TimeSpan Hour = TimeSpan.FromHours(1);
    private static readonly DateTimeOffset Origin = DateTimeOffset.UnixEpoch;
    private static readonly DateTimeOffset HourStart = new(2026, 11, 4, 12, 0, 0, TimeSpan.Zero);

    private readonly ServiceProvider _serviceProvider;
    private readonly AsyncServiceScope _scope;
    private readonly IReadOnlyDbContext _dbContext;

    public DatabaseFunctionsTests()
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
    public void DateBin_SourceOnBucketStart_ReturnsSource()
    {
        // Act
        var bucketStart = DatabaseFunctions.DateBin(Hour, HourStart, Origin);

        // Assert
        bucketStart.ShouldBe(HourStart);
    }

    [Fact]
    public void DateBin_SourceInsideBucket_ReturnsBucketStart()
    {
        // Act
        var bucketStart = DatabaseFunctions.DateBin(Hour, HourStart.AddMinutes(59).AddSeconds(59), Origin);

        // Assert
        bucketStart.ShouldBe(HourStart);
    }

    [Fact]
    public void DateBin_StrideNotDividingHour_AlignsWithOrigin()
    {
        // Arrange
        var stride = TimeSpan.FromMinutes(7);
        var origin = new DateTimeOffset(2026, 11, 4, 0, 0, 0, TimeSpan.Zero);

        // Act
        var bucketStart = DatabaseFunctions.DateBin(stride, origin.AddMinutes(20), origin);

        // Assert
        bucketStart.ShouldBe(origin.AddMinutes(14));
    }

    [Fact]
    public void DateBin_SourceBeforeOrigin_RoundsDown()
    {
        // Arrange
        var origin = HourStart;

        // Act
        var bucketStart = DatabaseFunctions.DateBin(Hour, origin.AddMinutes(-90), origin);

        // Assert
        bucketStart.ShouldBe(origin.AddHours(-2));
    }

    [Fact]
    public void DateBin_SourceWithOffset_ReturnsUtcBucketStart()
    {
        // Arrange
        var source = HourStart.AddMinutes(30).ToOffset(TimeSpan.FromHours(5.5));

        // Act
        var bucketStart = DatabaseFunctions.DateBin(Hour, source, Origin);

        // Assert
        bucketStart.Offset.ShouldBe(TimeSpan.Zero);
        bucketStart.ShouldBe(HourStart);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void DateBin_StrideNotPositive_Throws(long strideTicks)
    {
        // Act & Assert
        Should.Throw<ArgumentOutOfRangeException>(() => DatabaseFunctions.DateBin(TimeSpan.FromTicks(strideTicks), HourStart, Origin));
    }

    [Fact]
    public void DateBin_InQuery_TranslatesToPostgresDateBin()
    {
        // Act
        var sql = _dbContext.Query<MeterReading>()
            .Select(r => DatabaseFunctions.DateBin(Hour, r.ReceivedAt, Origin))
            .ToQueryString();

        // Assert
        sql.ShouldContain("date_bin(");
        sql.ShouldContain("m.received_at");
    }
}
