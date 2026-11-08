using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace ApiGateway.Api.Rest.Errors;

internal sealed class ApiExceptionHandler(
    IProblemDetailsService problemDetailsService,
    ILogger<ApiExceptionHandler> logger)
    : IExceptionHandler
{
    private const string InternalErrorMessage = "An unexpected error occurred.";

    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        logger.RequestFailed(exception, httpContext.Request.Method, httpContext.Request.Path.Value);

        httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;

        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Detail = InternalErrorMessage
            }
        });
    }
}

internal static partial class ApiExceptionHandlerLogs
{
    [LoggerMessage(Level = LogLevel.Error, Message = "{Method} {Path} failed with an unhandled exception")]
    public static partial void RequestFailed(this ILogger logger, Exception exception, string method, string? path);
}
