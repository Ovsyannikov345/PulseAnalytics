using System.Data.Common;
using HotChocolate;
using HotChocolate.Execution;

namespace ApiGateway.Api.GraphQL.Errors;

/// <summary>
/// Global error handling for GraphQL: resolver exceptions become <see cref="GraphQLErrorCodes.ServiceUnavailable"/>
/// (database failures and timeouts) or <see cref="GraphQLErrorCodes.InternalError"/> (anything else).
/// They are logged once, and their messages are hidden outside Development.
/// Errors without an exception and errors raised on purpose (<see cref="GraphQLException"/>) pass through unchanged.
/// </summary>
internal sealed class GraphQLErrorFilter(IHostEnvironment environment, ILogger<GraphQLErrorFilter> logger) : IErrorFilter
{
    private const string ServiceUnavailableMessage = "The service is temporarily unavailable. Try again later.";
    private const string InternalErrorMessage = "An unexpected error occurred.";

    public IError OnError(IError error)
    {
        var exception = error.Exception;

        if (exception is null or GraphQLException or OperationCanceledException)
        {
            return error;
        }

        return IsDatabaseFailure(exception)
            ? Replace(error, exception, GraphQLErrorCodes.ServiceUnavailable, ServiceUnavailableMessage)
            : Replace(error, exception, GraphQLErrorCodes.InternalError, InternalErrorMessage);
    }

    // The provider's exception is often wrapped: Npgsql's default execution strategy rethrows transient failures
    // (e.g. a lost connection) as an InvalidOperationException with the NpgsqlException inside.
    private static bool IsDatabaseFailure(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current is DbException or TimeoutException)
            {
                return true;
            }
        }

        return false;
    }

    private IError Replace(IError error, Exception exception, string code, string message)
    {
        logger.ResolverFailed(exception, code, error.Path?.ToString());

        return error
            .WithCode(code)
            .WithMessage(environment.IsDevelopment() ? exception.Message : message);
    }
}

internal static partial class GraphQLErrorFilterLogs
{
    [LoggerMessage(Level = LogLevel.Error, Message = "GraphQL resolver at {Path} failed, reported as {ErrorCode}")]
    public static partial void ResolverFailed(this ILogger logger, Exception exception, string errorCode, string? path);
}
