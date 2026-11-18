using NotificationService.Api.HealthCheck;
using NotificationService.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddInfrastructureServices();

builder.Services.AddHealthChecks();

var app = builder.Build();

app.MapHealthChecks(HealthCheckConstants.EndpointPath);

await app.RunAsync();
