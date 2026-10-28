using DataIngestor.IntegrationTests.Fixtures;
using Xunit;

// One RabbitMQ container is shared by the whole run.
[assembly: AssemblyFixture(typeof(RabbitMqContainerFixture))]

// Tests share the broker's exchange, and every host started from Program.cs replaces the static Serilog logger,
// so they run one at a time.
[assembly: CollectionBehavior(DisableTestParallelization = true)]

// Integration tests can be filtered out.
[assembly: Trait("Category", "Integration")]
