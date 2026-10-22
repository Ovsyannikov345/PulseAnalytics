using Ausiannikau.Utilities.Results;
using DataIngestor.Domain.Entities;

namespace DataIngestor.Application.Interfaces.Clients;

public interface IMetersClient
{
    public Task<Result<List<MeterReading>>> GetMetersAsync(CancellationToken ct);
}
