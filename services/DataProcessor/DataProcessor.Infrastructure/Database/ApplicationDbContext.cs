using DataProcessor.Application.Interfaces.Database;
using DataProcessor.Domain.Entities;
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
    }
}
