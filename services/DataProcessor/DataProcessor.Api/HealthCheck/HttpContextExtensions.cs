namespace DataProcessor.Api.HealthCheck;

public static class HttpContextExtensions
{
    public static bool IsHealthCheckRequest(this HttpContext context)
    {
        return context.Request.Path.StartsWithSegments(HealthCheckConstants.EndpointPath, StringComparison.OrdinalIgnoreCase);
    }
}
