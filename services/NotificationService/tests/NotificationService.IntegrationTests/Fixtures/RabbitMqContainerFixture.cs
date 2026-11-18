using System.Net.Http.Headers;
using System.Text;
using RabbitMQ.Client;
using Testcontainers.RabbitMq;
using Xunit;

namespace NotificationService.IntegrationTests.Fixtures;

/// <summary>
/// Starts a RabbitMQ broker in Docker once per test run.
/// </summary>
public sealed class RabbitMqContainerFixture : IAsyncLifetime
{
    private const string Image = "rabbitmq:4.1-management";

    private const int AmqpPort = 5672;

    // The service's queues are exclusive to its connection, so tests inspect them through the management API.
    private const int ManagementPort = 15672;

    private readonly RabbitMqContainer _container = new RabbitMqBuilder(Image)
        .WithPortBinding(ManagementPort, assignRandomHostPort: true)
        .Build();

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
            ClientProvidedName = "pulse-analytics.notification-service.integration-tests"
        };

        return await factory.CreateConnectionAsync(ct);
    }

    public HttpClient CreateManagementClient()
    {
        var credentials = Convert.ToBase64String(
            Encoding.UTF8.GetBytes($"{RabbitMqBuilder.DefaultUsername}:{RabbitMqBuilder.DefaultPassword}"));

        return new HttpClient
        {
            BaseAddress = new Uri($"http://{Host}:{_container.GetMappedPublicPort(ManagementPort)}/api/"),
            DefaultRequestHeaders = { Authorization = new AuthenticationHeaderValue("Basic", credentials) }
        };
    }
}
