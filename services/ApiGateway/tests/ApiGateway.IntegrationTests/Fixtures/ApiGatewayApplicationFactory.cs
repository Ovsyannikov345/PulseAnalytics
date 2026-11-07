using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace ApiGateway.IntegrationTests.Fixtures;

/// <summary>
/// Hosts the real gateway (Program.cs with all its registrations) against the test database.
/// </summary>
public sealed class ApiGatewayApplicationFactory(
    PostgreSqlContainerFixture postgreSql,
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
            ["Logging:LogLevel:Default"] = "Warning",

            ["Database:ConnectionString"] = postgreSql.ConnectionString
        };
    }
}
