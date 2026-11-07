using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore.Query;

namespace ApiGateway.UnitTests.TestDoubles;

/// <summary>
/// Wraps an in-memory collection in an <see cref="IQueryable{T}"/> that EF Core's async operators accept, both those
/// executed through the query provider (<c>CountAsync</c>, <c>FirstOrDefaultAsync</c>, ...) and async enumeration
/// (<c>ToListAsync</c>), so code that queries <c>IReadOnlyDbContext</c> can be tested without a database.
/// LINQ to Objects runs the query, so it checks the query's logic, not its SQL translation.
/// </summary>
internal static class AsyncQueryable
{
    public static IQueryable<T> AsAsyncQueryable<T>(this IEnumerable<T> items)
    {
        return new AsyncEnumerableQuery<T>(items);
    }

    private sealed class AsyncEnumerableQuery<T> : EnumerableQuery<T>, IQueryable<T>, IAsyncEnumerable<T>
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

        // Used by ToListAsync and other async enumeration.
        public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default)
        {
            return ((IEnumerable<T>)this).ToAsyncEnumerable().GetAsyncEnumerator(cancellationToken);
        }
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

        // EF Core asks for a Task<T> here, e.g. Task<int> for CountAsync.
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
