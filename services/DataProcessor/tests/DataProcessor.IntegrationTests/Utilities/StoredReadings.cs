using DataProcessor.Application.Interfaces.Database;
using DataProcessor.Domain.Entities.MeterReadings;
using DataProcessor.IntegrationTests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DataProcessor.IntegrationTests.Utilities;

/// <summary>
/// Reads the readings the processor stored, through its own database context.
/// </summary>
internal static class StoredReadings
{
    private static readonly TimeSpan StoreTimeout = TimeSpan.FromSeconds(30);

    public static async Task<IReadOnlyList<MeterReading>> GetAsync(
        ProcessorApplicationFactory factory,
        IReadOnlyCollection<Guid> deduplicationKeys,
        CancellationToken ct)
    {
        await using var scope = factory.Services.CreateAsyncScope();

        var dbContext = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();

        return await dbContext.Query<MeterReading>()
            .Where(reading => deduplicationKeys.Contains(reading.DeduplicationKey))
            .ToListAsync(ct);
    }

    public static async Task<IReadOnlyList<MeterReading>> WaitForAsync(
        ProcessorApplicationFactory factory,
        IReadOnlyCollection<Guid> deduplicationKeys,
        CancellationToken ct)
    {
        await TestWait.UntilAsync(
            async () => (await GetAsync(factory, deduplicationKeys, ct)).Count >= deduplicationKeys.Count,
            StoreTimeout,
            ct);

        return await GetAsync(factory, deduplicationKeys, ct);
    }

    public static async Task<int> CountAsync(ProcessorApplicationFactory factory, CancellationToken ct)
    {
        await using var scope = factory.Services.CreateAsyncScope();

        var dbContext = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();

        return await dbContext.Query<MeterReading>().CountAsync(ct);
    }
}
