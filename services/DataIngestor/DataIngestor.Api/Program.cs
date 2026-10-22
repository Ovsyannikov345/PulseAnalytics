using DataIngestor.Api;
using DataIngestor.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

var configuration = builder.Configuration;

if (builder.Environment.IsDevelopment())
{
    configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true);
}

builder.Services
    .AddInfrastructureServices(configuration)
    .AddApiServices()
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

app.Run();
