using Ausiannikau.Utilities.Results;
using HotChocolate;

namespace ApiGateway.Api.GraphQL.Errors;

internal static class ResultExtensions
{
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
