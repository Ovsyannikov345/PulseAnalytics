using DataProcessor.Domain.Entities;

namespace DataProcessor.Application.Interfaces.Database;

public interface IApplicationDbContext
{
    public IQueryable<TEntity> Query<TEntity>()
        where TEntity : BaseEntity;

    public void Add<TEntity>(TEntity entity)
        where TEntity : BaseEntity;

    public Task<int> SaveChangesAsync(CancellationToken ct);
}
