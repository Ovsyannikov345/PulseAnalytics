using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DataProcessor.Infrastructure.Database;

public static class DatabaseMigrationExtensions
{
    /// <summary>
    /// Applies the pending EF Core migrations to the database.
    /// </summary>
    public static async Task MigrateDatabaseAsync(this IServiceProvider serviceProvider, CancellationToken ct = default)
    {
        await using var scope = serviceProvider.CreateAsyncScope();

        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        await dbContext.Database.MigrateAsync(ct);
    }
}
