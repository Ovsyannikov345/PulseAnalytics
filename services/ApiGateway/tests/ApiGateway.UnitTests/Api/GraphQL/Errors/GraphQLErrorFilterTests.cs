using ApiGateway.Api.GraphQL.Errors;
using ApiGateway.UnitTests.TestDoubles;
using FakeItEasy;
using HotChocolate;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using Shouldly;
using Xunit;

namespace ApiGateway.UnitTests.Api.GraphQL.Errors;

public sealed class GraphQLErrorFilterTests
{
    private readonly FakeLogger<GraphQLErrorFilter> _logger = new();

    [Fact]
    public void OnError_NoException_ReturnsErrorUnchanged()
    {
        // Arrange
        var error = ErrorBuilder.New().SetMessage("The field `foo` does not exist.").SetCode("HC0020").Build();

        // Act
        var result = CreateSut(Environments.Production).OnError(error);

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
        var result = CreateSut(Environments.Production).OnError(error);

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
        var result = CreateSut(Environments.Production).OnError(error);

        // Assert
        result.ShouldBeSameAs(error);
        _logger.Collector.Count.ShouldBe(0);
    }

    [Fact]
    public void OnError_DatabaseException_ReturnsServiceUnavailable()
    {
        // Arrange
        var exception = new TestDbException("Failed to connect to 10.0.0.5:5432");

        // Act
        var result = CreateSut(Environments.Production).OnError(ErrorWith(exception));

        // Assert
        result.Code.ShouldBe(GraphQLErrorCodes.ServiceUnavailable);
        result.Message.ShouldBe("The service is temporarily unavailable. Try again later.");
    }

    [Fact]
    public void OnError_Timeout_ReturnsServiceUnavailable()
    {
        // Act
        var result = CreateSut(Environments.Production).OnError(ErrorWith(new TimeoutException("Command timed out")));

        // Assert
        result.Code.ShouldBe(GraphQLErrorCodes.ServiceUnavailable);
    }

    [Fact]
    public void OnError_WrappedDatabaseException_ReturnsServiceUnavailable()
    {
        // Arrange
        // What Npgsql's default execution strategy throws when the connection to the database is lost.
        var exception = new InvalidOperationException(
            "An exception has been raised that is likely due to a transient failure.",
            new TestDbException("Exception while reading from stream"));

        // Act
        var result = CreateSut(Environments.Production).OnError(ErrorWith(exception));

        // Assert
        result.Code.ShouldBe(GraphQLErrorCodes.ServiceUnavailable);
    }

    [Fact]
    public void OnError_UnexpectedException_ReturnsInternalErrorWithHiddenMessage()
    {
        // Act
        var result = CreateSut(Environments.Production).OnError(ErrorWith(new InvalidOperationException("Secret detail")));

        // Assert
        result.Code.ShouldBe(GraphQLErrorCodes.InternalError);
        result.Message.ShouldBe("An unexpected error occurred.");
    }

    [Fact]
    public void OnError_UnexpectedExceptionInDevelopment_ShowsExceptionMessage()
    {
        // Act
        var result = CreateSut(Environments.Development).OnError(ErrorWith(new InvalidOperationException("Secret detail")));

        // Assert
        result.Code.ShouldBe(GraphQLErrorCodes.InternalError);
        result.Message.ShouldBe("Secret detail");
    }

    [Fact]
    public void OnError_UnexpectedException_LogsErrorWithException()
    {
        // Arrange
        var exception = new InvalidOperationException("Secret detail");

        // Act
        CreateSut(Environments.Production).OnError(ErrorWith(exception));

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

    private GraphQLErrorFilter CreateSut(string environmentName)
    {
        var environment = A.Fake<IHostEnvironment>();
        A.CallTo(() => environment.EnvironmentName).Returns(environmentName);

        return new GraphQLErrorFilter(environment, _logger);
    }
}
