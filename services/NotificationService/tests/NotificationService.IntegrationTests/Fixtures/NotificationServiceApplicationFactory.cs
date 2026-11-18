using System.Globalization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Logging;
using Testcontainers.RabbitMq;

namespace NotificationService.IntegrationTests.Fixtures;

/// <summary>
/// Hosts the real notification service (Program.cs with all its registrations and consumers) against the test broker.
/// </summary>
public sealed class NotificationServiceApplicationFactory(
    RabbitMqContainerFixture rabbitMq,
    IReadOnlyDictionary<string, string?>? overrides = null)
    : WebApplicationFactory<Program>
{
    private const string EnvironmentName = "IntegrationTests";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(EnvironmentName);

        // Lets tests check what the background consumers report (see FakeLogCollector).
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
            ["Serilog:MinimumLevel:Default"] = "Warning",

            ["RabbitMq:Host"] = rabbitMq.Host,
            ["RabbitMq:Port"] = rabbitMq.Port.ToString(CultureInfo.InvariantCulture),
            ["RabbitMq:UserName"] = RabbitMqBuilder.DefaultUsername,
            ["RabbitMq:Password"] = RabbitMqBuilder.DefaultPassword
        };
    }
}
