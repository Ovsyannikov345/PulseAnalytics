namespace ApiGateway.Api.GraphQL.Errors;

/// <summary>
/// Values of <c>extensions.code</c> in GraphQL errors raised by the gateway. Errors raised by HotChocolate itself
/// (syntax, validation, paging arguments, timeouts) keep their own codes.
/// </summary>
internal static class GraphQLErrorCodes
{
    /// <summary>Invalid input (a failed <c>Result</c>); the GraphQL equivalent of <c>400 Bad Request</c>.</summary>
    public const string BadRequest = "BAD_REQUEST";

    /// <summary>The database is unreachable or timed out; the equivalent of <c>503 Service Unavailable</c>.</summary>
    public const string ServiceUnavailable = "SERVICE_UNAVAILABLE";

    /// <summary>Any other unexpected failure; the equivalent of <c>500 Internal Server Error</c>.</summary>
    public const string InternalError = "INTERNAL_ERROR";
}
