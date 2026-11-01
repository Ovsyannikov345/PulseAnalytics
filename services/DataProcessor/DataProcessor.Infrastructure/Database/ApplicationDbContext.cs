using DataProcessor.Application.Interfaces.Database;
using DataProcessor.Domain.Entities;
using DataProcessor.Infrastructure.Database.ValueGenerators;
using Microsoft.EntityFrameworkCore;

namespace DataProcessor.Infrastructure.Database;

internal sealed class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : DbContext(options), IApplicationDbContext
{
    public IQueryable<TEntity> Query<TEntity>()
        where TEntity : BaseEntity
    {
        return Set<TEntity>();
    }

    void IApplicationDbContext.Add<TEntity>(TEntity entity)
    {
        Set<TEntity>().Add(entity);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);

        var entityTypes = modelBuilder.Model.GetEntityTypes()
            .Where(entityType => typeof(BaseEntity).IsAssignableFrom(entityType.ClrType))
            .ToList();

        foreach (var entityType in entityTypes)
        {
            modelBuilder.Entity(entityType.ClrType)
                .Property(nameof(BaseEntity.Id))
                .HasValueGenerator<GuidVersion7ValueGenerator>();
        }
    }
}
