using ApiGateway.DataAccess;
using ApiGateway.DataAccess.Database;
using ApiGateway.DataAccess.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace ApiGateway.UnitTests.DataAccess.Database;

// No database is needed: the model, the generated SQL and the save guard are all checked before anything is sent.
// The context comes from the real AddDataAccess registration, so the production options are what is tested.
public sealed class ReadOnlyApplicationDbContextTests : IAsyncDisposable
{
    private const string ViewName = "meter_readings";
    private const string ViewSchema = "reporting";

    private readonly ServiceProvider _serviceProvider;
    private readonly AsyncServiceScope _scope;
    private readonly ReadOnlyApplicationDbContext _sut;

    public ReadOnlyApplicationDbContextTests()
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
        _sut = _scope.ServiceProvider.GetRequiredService<IReadOnlyDbContext>().ShouldBeOfType<ReadOnlyApplicationDbContext>();
    }

    public async ValueTask DisposeAsync()
    {
        await _scope.DisposeAsync();
        await _serviceProvider.DisposeAsync();
    }

    [Fact]
    public void Model_MeterReading_MapsToReportingViewOnly()
    {
        // Arrange
        var entityType = _sut.Model.FindEntityType(typeof(MeterReading)).ShouldNotBeNull();

        // Assert
        entityType.GetViewName().ShouldBe(ViewName);
        entityType.GetViewSchema().ShouldBe(ViewSchema);
        entityType.GetTableName().ShouldBeNull();
    }

    [Fact]
    public void Model_MeterReading_MatchesViewColumns()
    {
        // Arrange
        var entityType = _sut.Model.FindEntityType(typeof(MeterReading)).ShouldNotBeNull();
        var view = StoreObjectIdentifier.View(ViewName, ViewSchema);

        // Act
        var columns = entityType.GetProperties().Select(property => property.GetColumnName(view));

        // Assert
        columns.ShouldBe(
            ["id", "type", "location", "received_at", "motion_detected", "energy", "co2", "pm25", "humidity"],
            ignoreOrder: true);
    }

    [Fact]
    public void Query_MeterReadings_SelectsFromReportingView()
    {
        // Act
        var sql = _sut.Query<MeterReading>().ToQueryString();

        // Assert
        sql.ShouldContain($"FROM {ViewSchema}.{ViewName}");
    }

    [Fact]
    public void ChangeTracker_QueryTrackingBehavior_IsNoTracking()
    {
        // Assert
        _sut.ChangeTracker.QueryTrackingBehavior.ShouldBe(QueryTrackingBehavior.NoTracking);
    }

    [Fact]
    public void SaveChanges_Always_Throws()
    {
        // Act & Assert
        Should.Throw<InvalidOperationException>(() => _sut.SaveChanges());
    }

    [Fact]
    public async Task SaveChangesAsync_Always_Throws()
    {
        // Act & Assert
        await Should.ThrowAsync<InvalidOperationException>(() => _sut.SaveChangesAsync(TestContext.Current.CancellationToken));
    }
}
