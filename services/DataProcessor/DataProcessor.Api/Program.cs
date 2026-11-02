using DataProcessor.Api;
using DataProcessor.Api.HealthCheck;
using DataProcessor.Api.Observability;
using DataProcessor.Application;
using DataProcessor.Infrastructure;
using DataProcessor.Infrastructure.Database;
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
        .AddApplicationServices();

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

    await app.Services.MigrateDatabaseAsync(app.Lifetime.ApplicationStopping);

    app.MapHealthChecks(HealthCheckConstants.EndpointPath);

    await app.RunAsync();
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    Log.Fatal(ex, "Data processor terminated unexpectedly");
    Environment.ExitCode = 1;
}
finally
{
    await Log.CloseAndFlushAsync();
}
