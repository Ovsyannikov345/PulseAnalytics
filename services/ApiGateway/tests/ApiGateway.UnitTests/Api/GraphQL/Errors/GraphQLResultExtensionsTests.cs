using ApiGateway.Api.GraphQL.Errors;
using Ausiannikau.Utilities.Results;
using HotChocolate;
using Shouldly;
using Xunit;

namespace ApiGateway.UnitTests.Api.GraphQL.Errors;

public sealed class GraphQLResultExtensionsTests
{
    [Fact]
    public void GetValueOrThrow_SuccessfulResult_ReturnsValue()
    {
        // Act
        var value = Result<int>.Succeed(42).GetValueOrThrow();

        // Assert
        value.ShouldBe(42);
    }

    [Fact]
    public void GetValueOrThrow_FailedResult_ThrowsBadRequestError()
    {
        // Arrange
        var result = Result<int>.Fail("'From' must be earlier than 'To'.");

        // Act & Assert
        var exception = Should.Throw<GraphQLException>(() => result.GetValueOrThrow());

        var error = exception.Errors.ShouldHaveSingleItem();
        error.Code.ShouldBe(GraphQLErrorCodes.BadRequest);
        error.Message.ShouldBe("'From' must be earlier than 'To'.");
    }
}
