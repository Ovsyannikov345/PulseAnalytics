using ApiGateway.Api.HealthCheck;
using ApiGateway.BusinessLogic;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddBusinessLogic(builder.Configuration);

builder.Services.AddHealthChecks();

var app = builder.Build();

app.MapHealthChecks(HealthCheckConstants.EndpointPath);

await app.RunAsync();
