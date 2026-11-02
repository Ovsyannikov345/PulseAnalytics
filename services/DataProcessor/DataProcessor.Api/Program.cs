using DataProcessor.Api;
using DataProcessor.Api.Observability;
using DataProcessor.Application;
using DataProcessor.Infrastructure;
using DataProcessor.Infrastructure.Database;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    var configuration = builder.Configuration;

    builder.Services
        .AddSerilogLogging(configuration)
        .AddObservability(configuration)
        .AddInfrastructureServices(configuration)
        .AddApplicationServices();

    var app = builder.Build();

    app.UseSerilogRequestLogging();

    await app.Services.MigrateDatabaseAsync(app.Lifetime.ApplicationStopping);

    app.MapGet("/ping", () =>
    {
        return "pong";
    })
    .WithName("Ping");

    await app.RunAsync();
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    Log.Fatal(ex, "Data processor terminated unexpectedly");
    Environment.ExitCode = 1;
}
finally
{
    await Log.CloseAndFlushAsync();
}
