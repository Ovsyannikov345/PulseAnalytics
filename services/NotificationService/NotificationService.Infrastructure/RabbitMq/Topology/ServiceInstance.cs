namespace NotificationService.Infrastructure.RabbitMq.Topology;

/// <summary>
/// Identifies this running instance of the service. A new ID is created at every start.
/// </summary>
internal sealed record ServiceInstance
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
}
