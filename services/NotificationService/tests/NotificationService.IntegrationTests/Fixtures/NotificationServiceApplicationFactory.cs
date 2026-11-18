using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace NotificationService.IntegrationTests.Fixtures;

/// <summary>
/// Hosts the real notification service (Program.cs with all its registrations).
/// </summary>
public sealed class NotificationServiceApplicationFactory(IReadOnlyDictionary<string, string?>? overrides = null)
    : WebApplicationFactory<Program>
{
    private const string EnvironmentName = "IntegrationTests";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(EnvironmentName);

        foreach (var (key, value) in overrides ?? new Dictionary<string, string?>())
        {
            builder.UseSetting(key, value);
        }
    }
}
