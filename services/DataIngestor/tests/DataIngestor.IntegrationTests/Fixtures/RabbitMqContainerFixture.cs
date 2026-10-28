using RabbitMQ.Client;
using Testcontainers.RabbitMq;
using Xunit;

namespace DataIngestor.IntegrationTests.Fixtures;

/// <summary>
/// Starts a RabbitMQ broker in Docker once per test run.
/// </summary>
public sealed class RabbitMqContainerFixture : IAsyncLifetime
{
    private const string Image = "rabbitmq:4.1-management";

    private const int AmqpPort = 5672;

    private readonly RabbitMqContainer _container = new RabbitMqBuilder(Image).Build();

    public string Host => _container.Hostname;

    public int Port => _container.GetMappedPublicPort(AmqpPort);

    public async ValueTask InitializeAsync()
    {
        await _container.StartAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await _container.DisposeAsync();
    }

    public async Task<IConnection> CreateConnectionAsync(CancellationToken ct)
    {
        var factory = new ConnectionFactory
        {
            HostName = Host,
            Port = Port,
            UserName = RabbitMqBuilder.DefaultUsername,
            Password = RabbitMqBuilder.DefaultPassword,
            ClientProvidedName = "pulse-analytics.data-ingestor.integration-tests"
        };

        return await factory.CreateConnectionAsync(ct);
    }
}
