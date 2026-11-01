using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore.Query;

namespace DataProcessor.UnitTests.TestDoubles;

/// <summary>
/// Wraps an in-memory collection in an <see cref="IQueryable{T}"/> that EF Core's async operators executed through
/// the query provider (<c>AnyAsync</c>, <c>CountAsync</c>, <c>FirstOrDefaultAsync</c>, ...) accept, so code that queries
/// <c>IApplicationDbContext</c> can be tested without a database. Async enumeration (<c>ToListAsync</c>) is not supported.
/// </summary>
internal static class AsyncQueryable
{
    public static IQueryable<T> AsAsyncQueryable<T>(this IEnumerable<T> items)
    {
        return new AsyncEnumerableQuery<T>(items);
    }

    private sealed class AsyncEnumerableQuery<T> : EnumerableQuery<T>, IQueryable<T>
    {
        public AsyncEnumerableQuery(IEnumerable<T> items)
            : base(items)
        {
        }

        public AsyncEnumerableQuery(Expression expression)
            : base(expression)
        {
        }

        IQueryProvider IQueryable.Provider => new AsyncQueryProvider(this);
    }

    private sealed class AsyncQueryProvider(IQueryProvider inner) : IAsyncQueryProvider
    {
        public IQueryable CreateQuery(Expression expression)
        {
            var elementType = expression.Type.GetGenericArguments()[0];

            return (IQueryable)Activator.CreateInstance(typeof(AsyncEnumerableQuery<>).MakeGenericType(elementType), expression)!;
        }

        public IQueryable<TElement> CreateQuery<TElement>(Expression expression)
        {
            return new AsyncEnumerableQuery<TElement>(expression);
        }

        public object? Execute(Expression expression)
        {
            return inner.Execute(expression);
        }

        public TResult Execute<TResult>(Expression expression)
        {
            return inner.Execute<TResult>(expression);
        }

        // EF Core asks for a Task<T> here, e.g. Task<bool> for AnyAsync.
        public TResult ExecuteAsync<TResult>(Expression expression, CancellationToken cancellationToken)
        {
            var resultType = typeof(TResult).GetGenericArguments()[0];

            var result = typeof(IQueryProvider)
                .GetMethod(nameof(IQueryProvider.Execute), 1, [typeof(Expression)])!
                .MakeGenericMethod(resultType)
                .Invoke(inner, [expression]);

            return (TResult)typeof(Task)
                .GetMethod(nameof(Task.FromResult))!
                .MakeGenericMethod(resultType)
                .Invoke(null, [result])!;
        }
    }
}
