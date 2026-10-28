using DataIngestor.IntegrationTests.Fixtures;
using Xunit;

// One RabbitMQ container is shared by the whole run.
[assembly: AssemblyFixture(typeof(RabbitMqContainerFixture))]

// Tests share the broker's exchange, and every host started from Program.cs replaces the static Serilog logger,
// so they run one at a time.
[assembly: CollectionBehavior(DisableTestParallelization = true)]

// CI runs unit and integration tests in separate steps, selected by this trait: --filter "Category=Integration".
[assembly: Trait("Category", "Integration")]
