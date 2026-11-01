using Npgsql;
using Testcontainers.PostgreSql;
using Xunit;

namespace DataProcessor.IntegrationTests.Fixtures;

/// <summary>
/// Starts a PostgreSQL server in Docker once per test run.
/// </summary>
public sealed class PostgreSqlContainerFixture : IAsyncLifetime
{
    private const string Image = "postgres:18.6-alpine";

    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder(Image).Build();

    public string ConnectionString => _container.GetConnectionString();

    public async ValueTask InitializeAsync()
    {
        await _container.StartAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await _container.DisposeAsync();
    }

    public async Task<NpgsqlConnection> CreateConnectionAsync(CancellationToken ct)
    {
        var connection = new NpgsqlConnection(ConnectionString);

        await connection.OpenAsync(ct);

        return connection;
    }
}
