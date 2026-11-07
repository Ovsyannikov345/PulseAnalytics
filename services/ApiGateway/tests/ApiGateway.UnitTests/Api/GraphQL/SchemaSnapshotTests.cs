using ApiGateway.BusinessLogic.MeterReadings;
using ApiGateway.UnitTests.Utilities;
using FakeItEasy;
using Shouldly;
using Xunit;

namespace ApiGateway.UnitTests.Api.GraphQL;

// ApiGateway.Api/GraphQL/schema.graphql is the committed public contract (the frontend can generate types from it).
// A schema change fails this test until the file is updated on purpose: the printed schema is written next to the test
// binaries as schema.actual.graphql, so it can be compared and copied over the committed file.
public sealed class SchemaSnapshotTests : IAsyncDisposable
{
    private static readonly string SnapshotPath = Path.Combine(AppContext.BaseDirectory, "Schema", "schema.graphql");
    private static readonly string ActualPath = Path.Combine(AppContext.BaseDirectory, "Schema", "schema.actual.graphql");

    private readonly GraphQLTestServer _server = new(A.Fake<IMeterReadingsService>());

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
        if (actual != expected)
        {
            await File.WriteAllTextAsync(ActualPath, actual, ct);
        }

        actual.ShouldBe(expected, $"The GraphQL schema changed. If intended, copy {ActualPath} over ApiGateway.Api/GraphQL/schema.graphql.");
    }

    // The committed file may be checked out with CRLF line endings.
    private static string Normalize(string schema)
    {
        return schema.ReplaceLineEndings("\n").Trim() + "\n";
    }
}
