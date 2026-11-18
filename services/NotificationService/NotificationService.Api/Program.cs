using NotificationService.Api;
using NotificationService.Api.HealthCheck;
using NotificationService.Api.Hubs.Readings;
using NotificationService.Api.Observability;
using NotificationService.Infrastructure;
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
        .AddInfrastructureServices()
        .AddReadingsHub();

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

    app.MapHealthChecks(HealthCheckConstants.EndpointPath);

    app.MapHub<ReadingsHub>(ReadingsHub.Route);

    await app.RunAsync();
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    Log.Fatal(ex, "Notification service terminated unexpectedly");
    Environment.ExitCode = 1;
}
finally
{
    await Log.CloseAndFlushAsync();
}
