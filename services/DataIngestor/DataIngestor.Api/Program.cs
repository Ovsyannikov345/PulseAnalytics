using DataIngestor.Api;
using DataIngestor.Api.Observability;
using DataIngestor.Application;
using DataIngestor.Background;
using DataIngestor.Infrastructure;
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
        .AddApplicationServices()
        .AddBackgroundServices(configuration)
        .AddApiServices();

    var app = builder.Build();

    app.UseSerilogRequestLogging();

    if (app.Environment.IsDevelopment())
    {
        app.MapOpenApi();
    }

    app.MapGet("/ping", () =>
    {
        return "pong";
    })
    .WithName("Ping");

    await app.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Data ingestor terminated unexpectedly");
    Environment.ExitCode = 1;
}
finally
{
    await Log.CloseAndFlushAsync();
}
