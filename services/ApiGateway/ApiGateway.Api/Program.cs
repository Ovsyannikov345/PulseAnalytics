using ApiGateway.Api;
using ApiGateway.Api.HealthCheck;
using ApiGateway.BusinessLogic;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddBusinessLogic(builder.Configuration)
    .AddGraphQLApi();

builder.Services.AddHealthChecks();

var app = builder.Build();

app.MapHealthChecks(HealthCheckConstants.EndpointPath);
app.MapGraphQL();

await app.RunWithGraphQLCommandsAsync(args);
