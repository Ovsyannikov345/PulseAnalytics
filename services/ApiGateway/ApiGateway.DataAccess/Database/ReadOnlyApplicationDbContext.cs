using ApiGateway.DataAccess.Entities;
using Microsoft.EntityFrameworkCore;

namespace ApiGateway.DataAccess.Database;

public interface IReadOnlyDbContext
{
    public IQueryable<TEntity> Query<TEntity>() where TEntity : BaseEntity;
}

internal sealed class ReadOnlyApplicationDbContext(DbContextOptions<ReadOnlyApplicationDbContext> options)
    : DbContext(options), IReadOnlyDbContext
{
    private const string ReadOnlyMessage = "The API gateway's database context is read-only.";

    public IQueryable<TEntity> Query<TEntity>() where TEntity : BaseEntity
    {
        return Set<TEntity>();
    }

    // The parameterless overloads delegate to these two, so every save path throws.
    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        throw new InvalidOperationException(ReadOnlyMessage);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        throw new InvalidOperationException(ReadOnlyMessage);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ReadOnlyApplicationDbContext).Assembly);
    }
}
