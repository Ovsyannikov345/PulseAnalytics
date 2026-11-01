using DataProcessor.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

var configuration = builder.Configuration;

builder.Services.AddInfrastructureServices(configuration);

var app = builder.Build();

await app.Services.MigrateDatabaseAsync(app.Lifetime.ApplicationStopping);

app.MapGet("/ping", () =>
{
    return "pong";
})
.WithName("Ping");

await app.RunAsync();
