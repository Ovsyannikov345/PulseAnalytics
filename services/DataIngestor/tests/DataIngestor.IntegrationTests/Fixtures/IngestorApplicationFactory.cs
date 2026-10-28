using System.Globalization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace DataIngestor.IntegrationTests.Fixtures;

/// <summary>
/// Hosts the real ingestor (Program.cs with all its registrations) against the test broker and the WeakApp stub.
/// </summary>
public sealed class IngestorApplicationFactory(
    RabbitMqContainerFixture rabbitMq,
    WeakAppStub weakApp,
    IReadOnlyDictionary<string, string?>? overrides = null)
    : WebApplicationFactory<Program>
{
    private const string EnvironmentName = "IntegrationTests";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(EnvironmentName);

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

            ["WeakApp:BaseUrl"] = weakApp.BaseUrl,
            ["WeakApp:ApiKey"] = WeakAppStub.ApiKey,
            ["WeakApp:Resilience:TotalTimeoutInSeconds"] = "30",
            ["WeakApp:Resilience:AttemptTimeoutInSeconds"] = "1",
            ["WeakApp:Resilience:MaxRetryAttempts"] = "2",
            ["WeakApp:Resilience:RetryBaseDelayInSeconds"] = "1",

            ["RabbitMq:Host"] = rabbitMq.Host,
            ["RabbitMq:Port"] = rabbitMq.Port.ToString(CultureInfo.InvariantCulture),
            ["RabbitMq:UserName"] = rabbitMq.UserName,
            ["RabbitMq:Password"] = rabbitMq.Password,
            ["RabbitMq:PublishTimeoutInSeconds"] = "5",

            ["MeterPollingWorker:StartupDelayInSeconds"] = "300",
            ["MeterPollingWorker:PollingPeriodInSeconds"] = "3600"
        };
    }
}
