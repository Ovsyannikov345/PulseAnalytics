using ApiGateway.BusinessLogic.MeterReadings;
using ApiGateway.UnitTests.Utilities;
using FakeItEasy;
using Shouldly;
using Xunit;

namespace ApiGateway.UnitTests.Api.GraphQL;

// ApiGateway.Api/GraphQL/schema.graphql is the committed public contract (the frontend can generate types from it).
// A schema change fails this test until the file is updated on purpose with the gateway's export command.
public sealed class SchemaSnapshotTests : IAsyncDisposable
{
    // dotnet run starts the app in the project folder, so the output path is relative to ApiGateway.Api.
    private const string ExportCommand = "dotnet run --project ApiGateway.Api -- schema export --output GraphQL/schema.graphql";

    private static readonly string SnapshotPath = Path.Combine(AppContext.BaseDirectory, "Schema", "schema.graphql");

    private readonly GraphQLTestServer _server = new(A.Fake<IMeterReadingsService>(), A.Fake<IReadingStatisticsService>());

    public async ValueTask DisposeAsync()
    {
        await _server.DisposeAsync();
    }

    [Fact]
    public async Task Schema_MatchesCommittedSnapshot()
    {
        // Arrange
        var executor = await _server.GetExecutorAsync();
        var ct = TestContext.Current.CancellationToken;

        var expected = Normalize(await File.ReadAllTextAsync(SnapshotPath, ct));

        // Act
        var actual = Normalize(executor.Schema.ToString());

        // Assert
        actual.ShouldBe(expected, $"The GraphQL schema changed. If intended, run from services/ApiGateway: {ExportCommand}");
    }

    // The committed file may be checked out with CRLF line endings.
    private static string Normalize(string schema)
    {
        return schema.ReplaceLineEndings("\n").Trim() + "\n";
    }
}
