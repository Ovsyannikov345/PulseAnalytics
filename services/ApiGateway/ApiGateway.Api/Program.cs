using ApiGateway.Api;
using ApiGateway.Api.HealthCheck;
using ApiGateway.BusinessLogic;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddBusinessLogic(builder.Configuration)
    .AddGraphQLApi()
    .AddRestApi();

builder.Services.AddHealthChecks();

var app = builder.Build();

app.UseExceptionHandler();

app.MapHealthChecks(HealthCheckConstants.EndpointPath);
app.MapGraphQL();
app.MapControllers();

app.UseOpenApiDocumentation();

await app.RunWithGraphQLCommandsAsync(args);
