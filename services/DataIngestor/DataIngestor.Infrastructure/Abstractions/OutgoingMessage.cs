namespace DataIngestor.Infrastructure.Abstractions;

internal sealed record OutgoingMessage(string Exchange, string RoutingKey, Guid MessageId, object Body)
{
    public string MessageType => Body.GetType().Name;
}
