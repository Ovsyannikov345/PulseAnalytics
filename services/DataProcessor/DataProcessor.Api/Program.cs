using DataProcessor.Application;
using DataProcessor.Infrastructure;
using DataProcessor.Infrastructure.Database;

var builder = WebApplication.CreateBuilder(args);

var configuration = builder.Configuration;

builder.Services
    .AddInfrastructureServices(configuration)
    .AddApplicationServices();

var app = builder.Build();

await app.Services.MigrateDatabaseAsync(app.Lifetime.ApplicationStopping);

app.MapGet("/ping", () =>
{
    return "pong";
})
.WithName("Ping");

await app.RunAsync();
