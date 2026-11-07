using ApiGateway.IntegrationTests.Fixtures;
using Xunit;

// One PostgreSQL container, migrated by the data processor's image, is shared by the whole run.
[assembly: AssemblyFixture(typeof(PostgreSqlContainerFixture))]

// Tests share the readings table and clear it before each test, so they run one at a time.
[assembly: CollectionBehavior(DisableTestParallelization = true)]

// CI runs unit and integration tests in separate steps, selected by this trait: --filter "Category=Integration".
[assembly: Trait("Category", "Integration")]
