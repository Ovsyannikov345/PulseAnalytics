using DataIngestor.Api;
using DataIngestor.Api.HealthCheck;
using DataIngestor.Api.Observability;
using DataIngestor.Application;
using DataIngestor.Background;
using DataIngestor.Infrastructure;
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
        .AddInfrastructureServices(configuration)
        .AddApplicationServices()
        .AddBackgroundServices(configuration)
        .AddApiServices();

    builder.Services.AddHealthChecks();

    var app = builder.Build();

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

    if (app.Environment.IsDevelopment())
    {
        app.MapOpenApi();
    }

    app.MapHealthChecks(HealthCheckConstants.EndpointPath);

    await app.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Data ingestor terminated unexpectedly");
    Environment.ExitCode = 1;
}
finally
{
    await Log.CloseAndFlushAsync();
}
