using DataProcessor.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

var configuration = builder.Configuration;

builder.Services.AddInfrastructureServices(configuration);

var app = builder.Build();

app.MapGet("/ping", () =>
{
    return "pong";
})
.WithName("Ping");

await app.RunAsync();
