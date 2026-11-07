using System.Data.Common;
using HotChocolate;
using HotChocolate.Execution;

namespace ApiGateway.Api.GraphQL.Errors;

internal sealed class GraphQLErrorFilter(ILogger<GraphQLErrorFilter> logger) : IErrorFilter
{
    private const string InternalErrorMessage = "An unexpected error occurred.";

    public IError OnError(IError error)
    {
        var exception = error.Exception;

        if (exception is null or GraphQLException or OperationCanceledException)
        {
            return error;
        }

        logger.ResolverFailed(exception, GraphQLErrorCodes.InternalError, error.Path?.ToString());

        return error
            .WithCode(GraphQLErrorCodes.InternalError)
            .WithMessage(InternalErrorMessage);
    }
}

internal static partial class GraphQLErrorFilterLogs
{
    [LoggerMessage(Level = LogLevel.Error, Message = "GraphQL resolver at {Path} failed, reported as {ErrorCode}")]
    public static partial void ResolverFailed(this ILogger logger, Exception exception, string errorCode, string? path);
}
