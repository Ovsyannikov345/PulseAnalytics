using System.Text.Json;
using ApiGateway.Api.GraphQL.Errors;
using ApiGateway.UnitTests.TestDoubles;
using HotChocolate;
using HotChocolate.Language;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using Shouldly;
using Xunit;

namespace ApiGateway.UnitTests.Api.GraphQL.Errors;

public sealed class GraphQLErrorFilterTests
{
    private readonly FakeLogger<GraphQLErrorFilter> _logger = new();
    private readonly GraphQLErrorFilter _sut;

    public GraphQLErrorFilterTests()
    {
        _sut = new GraphQLErrorFilter(_logger);
    }

    [Fact]
    public void OnError_NoException_ReturnsErrorUnchanged()
    {
        // Arrange
        var error = ErrorBuilder.New().SetMessage("The field `foo` does not exist.").SetCode("HC0020").Build();

        // Act
        var result = _sut.OnError(error);

        // Assert
        result.ShouldBeSameAs(error);
        _logger.Collector.Count.ShouldBe(0);
    }

    [Fact]
    public void OnError_GraphQLException_ReturnsErrorUnchanged()
    {
        // Arrange
        var error = ErrorWith(new GraphQLException("'From' must be earlier than 'To'."));

        // Act
        var result = _sut.OnError(error);

        // Assert
        result.ShouldBeSameAs(error);
        _logger.Collector.Count.ShouldBe(0);
    }

    [Fact]
    public void OnError_OperationCanceled_ReturnsErrorUnchangedWithoutLogging()
    {
        // Arrange
        var error = ErrorWith(new OperationCanceledException());

        // Act
        var result = _sut.OnError(error);

        // Assert
        result.ShouldBeSameAs(error);
        _logger.Collector.Count.ShouldBe(0);
    }

    [Theory]
    [InlineData("Request must contain either a query or a document id.", false)]
    [InlineData("Invalid JSON document.", true)]
    public void OnError_InvalidGraphQLRequest_ReturnsBadRequestWithOriginalMessage(string message, bool wrapsJsonError)
    {
        // Arrange
        var error = InvalidRequestError(message, wrapsJsonError);

        // Act
        var result = _sut.OnError(error);

        // Assert
        result.Code.ShouldBe(GraphQLErrorCodes.BadRequest);
        result.Message.ShouldBe(message);
        result.Exception.ShouldBeNull();
    }

    [Fact]
    public void OnError_InvalidGraphQLRequest_DoesNotLog()
    {
        // Act
        _sut.OnError(InvalidRequestError("Invalid JSON document.", wrapsJsonError: true));

        // Assert
        _logger.Collector.Count.ShouldBe(0);
    }

    [Fact]
    public void OnError_UnexpectedException_ReturnsInternalErrorWithHiddenMessage()
    {
        // Act
        var result = _sut.OnError(ErrorWith(new InvalidOperationException("Secret detail")));

        // Assert
        result.Code.ShouldBe(GraphQLErrorCodes.InternalError);
        result.Message.ShouldBe("An unexpected error occurred.");
    }

    [Fact]
    public void OnError_DatabaseException_ReturnsInternalErrorWithHiddenMessage()
    {
        // Act
        var result = _sut.OnError(ErrorWith(new TestDbException("Failed to connect to 10.0.0.5:5432")));

        // Assert
        result.Code.ShouldBe(GraphQLErrorCodes.InternalError);
        result.Message.ShouldBe("An unexpected error occurred.");
    }

    [Fact]
    public void OnError_UnexpectedException_LogsErrorWithException()
    {
        // Arrange
        var exception = new InvalidOperationException("Secret detail");

        // Act
        _sut.OnError(ErrorWith(exception));

        // Assert
        var record = _logger.Collector.GetSnapshot().ShouldHaveSingleItem();
        record.Level.ShouldBe(LogLevel.Error);
        record.Exception.ShouldBeSameAs(exception);
        record.Message.ShouldContain(GraphQLErrorCodes.InternalError);
        record.Message.ShouldContain("readings");
    }

    private static IError ErrorWith(Exception exception)
    {
        return ErrorBuilder.New()
            .SetMessage("Unexpected Execution Error")
            .SetException(exception)
            .SetPath(HotChocolate.Path.Root.Append("readings"))
            .Build();
    }

    // Shaped like the error HotChocolate builds when it can't parse the request body: its message, its own code and
    // the parser's exception, with no path.
    private static IError InvalidRequestError(string message, bool wrapsJsonError)
    {
        var exception = wrapsJsonError
            ? new InvalidGraphQLRequestException(message, new JsonException("'}' is invalid after a property name."))
            : new InvalidGraphQLRequestException(message);

        return ErrorBuilder.New()
            .SetMessage(message)
            .SetCode("HC0012")
            .SetException(exception)
            .Build();
    }
}
