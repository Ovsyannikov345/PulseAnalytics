using System.Text.Json;
using ApiGateway.Api;
using ApiGateway.BusinessLogic.MeterReadings;
using HotChocolate;
using HotChocolate.Execution;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ApiGateway.UnitTests.Utilities;

/// <summary>
/// The gateway's real GraphQL schema and request pipeline (<c>AddGraphQLApi</c>) over fake services,
/// without HTTP or a database.
/// </summary>
internal sealed class GraphQLTestServer(
    IMeterReadingsService meterReadingsService,
    IReadingStatisticsService readingStatisticsService)
    : IAsyncDisposable
{
    private readonly ServiceProvider _serviceProvider = new ServiceCollection()
        .AddLogging()
        .AddSingleton(meterReadingsService)
        .AddSingleton(readingStatisticsService)
        .AddGraphQLApi()
        .BuildServiceProvider();

    public async Task<IRequestExecutor> GetExecutorAsync()
    {
        return await _serviceProvider.GetRequestExecutorAsync(cancellationToken: TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// Runs <paramref name="query"/> and returns the response as JSON, as a client would receive it.
    /// </summary>
    public async Task<JsonElement> ExecuteAsync(string query)
    {
        var executor = await GetExecutorAsync();

        var result = await executor.ExecuteAsync(query, TestContext.Current.CancellationToken);

        return JsonDocument.Parse(result.ToJson()).RootElement.Clone();
    }

    public async ValueTask DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();
    }
}
