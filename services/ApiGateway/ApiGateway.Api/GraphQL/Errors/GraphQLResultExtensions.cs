using Ausiannikau.Utilities.Results;
using HotChocolate;

namespace ApiGateway.Api.GraphQL.Errors;

internal static class GraphQLResultExtensions
{
    /// <summary>
    /// Returns the value of a successful result. A failed result means invalid input,
    /// so it becomes a GraphQL error with the <see cref="GraphQLErrorCodes.BadRequest"/> code and the result's message.
    /// </summary>
    public static T GetValueOrThrow<T>(this Result<T> result)
    {
        if (result.TryGetSuccessValue(out var value))
        {
            return value;
        }

        throw new GraphQLException(ErrorBuilder.New()
            .SetMessage(result.ErrorMessage)
            .SetCode(GraphQLErrorCodes.BadRequest)
            .Build());
    }
}
