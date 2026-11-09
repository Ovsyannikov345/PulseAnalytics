using ApiGateway.Api;
using ApiGateway.Api.HealthCheck;
using ApiGateway.Api.Observability;
using ApiGateway.BusinessLogic;
using ApiGateway.BusinessLogic.Telemetry.Metrics;
using Serilog;
using Serilog.Events;

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
        .AddBusinessLogic(configuration)
        .AddGraphQLApi()
        .AddRestApi()
        .AddCrossOriginRequests(configuration);

    builder.Services.AddHealthChecks();

    var app = builder.Build();

    // Creates the custom instruments at startup.
    _ = app.Services.GetRequiredService<IMetricsService>();

    app.UseSerilogRequestLogging(options =>
    {
        options.GetLevel = (context, _, exception) =>
        {
            if (exception is not null || context.Response.StatusCode >= StatusCodes.Status500InternalServerError)
            {
                return LogEventLevel.Error;
            }

            return context.IsHealthCheckRequest() ? LogEventLevel.Verbose : LogEventLevel.Information;
        };
    });

    app.UseExceptionHandler();

    app.UseCors();

    app.MapHealthChecks(HealthCheckConstants.EndpointPath);
    app.MapGraphQL();
    app.MapControllers();

    app.UseOpenApiDocumentation();

    await app.RunWithGraphQLCommandsAsync(args);
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    Log.Fatal(ex, "API gateway terminated unexpectedly");
    Environment.ExitCode = 1;
}
finally
{
    await Log.CloseAndFlushAsync();
}
