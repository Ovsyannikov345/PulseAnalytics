using Ausiannikau.Utilities.Results;
using DataProcessor.Domain.Entities.MeterReadings;

namespace DataProcessor.Application.Interfaces.Messaging;

public interface IStoredReadingsPublisher
{
    public Task<Result> PublishAsync(MeterReading reading, CancellationToken ct);
}
