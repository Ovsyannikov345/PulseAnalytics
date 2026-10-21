using Ausiannikau.Utilities.Results;
using DataIngestor.Application.ExternalMeters.Dto;

namespace DataIngestor.Application.Interfaces.Clients;

public interface IMetersClient
{
    public Task<Result<List<MeterReading>>> GetMetersAsync(CancellationToken ct);
}
