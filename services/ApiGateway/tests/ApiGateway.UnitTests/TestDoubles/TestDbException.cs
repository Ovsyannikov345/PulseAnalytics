using System.Data.Common;

namespace ApiGateway.UnitTests.TestDoubles;

/// <summary>
/// Stands in for <c>NpgsqlException</c>: the Api only knows the provider-neutral <see cref="DbException"/> base class.
/// </summary>
internal sealed class TestDbException(string message) : DbException(message);
