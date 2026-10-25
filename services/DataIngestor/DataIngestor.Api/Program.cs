using DataIngestor.Api;
using DataIngestor.Application;
using DataIngestor.Background;
using DataIngestor.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

var configuration = builder.Configuration;

builder.Services
    .AddInfrastructureServices(configuration)
    .AddApplicationServices()
    .AddBackgroundServices(configuration)
    .AddApiServices();

var app = builder.Build();

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
