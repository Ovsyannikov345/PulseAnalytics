using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Images;
using DotNet.Testcontainers.Networks;
using Npgsql;
using Testcontainers.PostgreSql;
using Xunit;

namespace ApiGateway.IntegrationTests.Fixtures;

/// <summary>
/// Starts a PostgreSQL server in Docker once per test run, with the schema created by the data processor's image.
/// The processor owns the schema and its migrations; the gateway only reads the <c>reporting.meter_readings</c> view,
/// so a processor change that breaks the view fails these tests.
/// </summary>
public sealed class PostgreSqlContainerFixture : IAsyncLifetime
{
    private const string PostgreSqlImage = "postgres:18.6-alpine";
    private const string NetworkAlias = "postgres";
    private const int PostgreSqlPort = 5432;
    private const string Database = "pulse_analytics";
    private const string UserName = "postgres";
    private const string Password = "postgres";

    private const string ProcessorImage = "ilyaovsyannikov/pulse-analytics-data-processor";

    // Same variable as in deploy/docker-compose.yml, so a run can pin the processor version it is checked against.
    private const string ProcessorTagVariable = "DATA_PROCESSOR_TAG";
    private const string DefaultProcessorTag = "latest";
    private const ushort ProcessorHttpPort = 8080;
    private const string ProcessorHealthPath = "/health";

    private static readonly TimeSpan MigrationTimeout = TimeSpan.FromMinutes(2);

    private readonly INetwork _network;
    private readonly PostgreSqlContainer _container;

    public PostgreSqlContainerFixture()
    {
        _network = new NetworkBuilder().Build();

        _container = new PostgreSqlBuilder(PostgreSqlImage)
            .WithNetwork(_network)
            .WithNetworkAliases(NetworkAlias)
            .WithDatabase(Database)
            .WithUsername(UserName)
            .WithPassword(Password)
            .Build();
    }

    public string ConnectionString => _container.GetConnectionString();

    public async ValueTask InitializeAsync()
    {
        await _network.CreateAsync();
        await _container.StartAsync();
        await MigrateWithProcessorAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await _container.DisposeAsync();
        await _network.DisposeAsync();
    }

    public async Task<NpgsqlConnection> CreateConnectionAsync(CancellationToken ct)
    {
        var connection = new NpgsqlConnection(ConnectionString);

        await connection.OpenAsync(ct);

        return connection;
    }

    /// <summary>
    /// Runs the processor once: it migrates the database on start and answers <c>/health</c> only afterwards,
    /// so a healthy processor means the schema exists. It is stopped right after, as nothing else needs it.
    /// </summary>
    private async Task MigrateWithProcessorAsync()
    {
        var tag = Environment.GetEnvironmentVariable(ProcessorTagVariable) is { Length: > 0 } value
            ? value
            : DefaultProcessorTag;

        await using var processor = new ContainerBuilder($"{ProcessorImage}:{tag}")
            // A stale local image could hide a change of the read contract.
            .WithImagePullPolicy(PullPolicy.Always)
            .WithNetwork(_network)
            .WithEnvironment(
                "Database__ConnectionString",
                $"Host={NetworkAlias};Port={PostgreSqlPort};Database={Database};Username={UserName};Password={Password}")
            // No broker is started: the processor's consumers keep retrying, its host and /health still come up.
            .WithEnvironment("RabbitMq__Host", "rabbitmq")
            .WithEnvironment("RabbitMq__UserName", "guest")
            .WithEnvironment("RabbitMq__Password", "guest")
            .WithEnvironment("Serilog__MinimumLevel__Default", "Warning")
            .WithPortBinding(ProcessorHttpPort, assignRandomHostPort: true)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilHttpRequestIsSucceeded(
                request => request.ForPort(ProcessorHttpPort).ForPath(ProcessorHealthPath),
                strategy => strategy.WithTimeout(MigrationTimeout)))
            .Build();

        await processor.StartAsync();
    }
}
