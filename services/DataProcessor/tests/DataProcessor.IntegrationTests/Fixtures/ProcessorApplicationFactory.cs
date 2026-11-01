using System.Globalization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Logging;
using Testcontainers.RabbitMq;

namespace DataProcessor.IntegrationTests.Fixtures;

/// <summary>
/// Hosts the real processor (Program.cs with all its registrations, database migration and consumers)
/// against the test broker and database.
/// </summary>
public sealed class ProcessorApplicationFactory(
    RabbitMqContainerFixture rabbitMq,
    PostgreSqlContainerFixture postgreSql,
    IReadOnlyDictionary<string, string?>? overrides = null)
    : WebApplicationFactory<Program>
{
    private const string EnvironmentName = "IntegrationTests";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(EnvironmentName);

        // Lets tests wait for what the background consumers report (see FakeLogCollector).
        builder.ConfigureLogging(logging => logging.AddFakeLogging());

        foreach (var (key, value) in DefaultSettings())
        {
            builder.UseSetting(key, value);
        }

        foreach (var (key, value) in overrides ?? new Dictionary<string, string?>())
        {
            builder.UseSetting(key, value);
        }
    }

    private Dictionary<string, string?> DefaultSettings()
    {
        return new Dictionary<string, string?>
        {
            ["Logging:LogLevel:Default"] = "Warning",

            ["RabbitMq:Host"] = rabbitMq.Host,
            ["RabbitMq:Port"] = rabbitMq.Port.ToString(CultureInfo.InvariantCulture),
            ["RabbitMq:UserName"] = RabbitMqBuilder.DefaultUsername,
            ["RabbitMq:Password"] = RabbitMqBuilder.DefaultPassword,

            ["Database:ConnectionString"] = postgreSql.ConnectionString
        };
    }
}
