using Ausiannikau.Utilities.Results;
using DataIngestor.Domain.Entities;

namespace DataIngestor.Application.Interfaces.Messaging;

public interface IMeterReadingsPublisher
{
    public Task<Result> PublishAsync(IReadOnlyCollection<MeterReading> readings, CancellationToken ct);
}
