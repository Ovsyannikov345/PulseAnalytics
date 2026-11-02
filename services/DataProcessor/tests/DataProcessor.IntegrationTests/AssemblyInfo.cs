using DataProcessor.IntegrationTests.Fixtures;
using Xunit;

// One RabbitMQ and one PostgreSQL container are shared by the whole run.
[assembly: AssemblyFixture(typeof(RabbitMqContainerFixture))]
[assembly: AssemblyFixture(typeof(PostgreSqlContainerFixture))]

// Tests share the broker's durable processing queues, only one host may consume them at a time,
// and every host started from Program.cs replaces the static Serilog logger, so they run one at a time.
[assembly: CollectionBehavior(DisableTestParallelization = true)]

// CI runs unit and integration tests in separate steps, selected by this trait: --filter "Category=Integration".
[assembly: Trait("Category", "Integration")]
