using System.Text.Json;
using ApiGateway.Api.Rest.Errors;
using ApiGateway.UnitTests.TestDoubles;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using Shouldly;
using Xunit;

namespace ApiGateway.UnitTests.Api.Rest.Errors;

public sealed class ApiExceptionHandlerTests : IAsyncDisposable
{
    private readonly FakeLogger<ApiExceptionHandler> _logger = new();
    private readonly MemoryStream _body = new();
    private readonly ServiceProvider _services;
    private readonly DefaultHttpContext _httpContext;
    private readonly ApiExceptionHandler _sut;

    public ApiExceptionHandlerTests()
    {
        // The real ProblemDetails writer, as registered by AddProblemDetails.
        _services = new ServiceCollection()
            .AddLogging()
            .AddProblemDetails()
            .BuildServiceProvider();

        _httpContext = new DefaultHttpContext { RequestServices = _services };
        _httpContext.Request.Method = HttpMethods.Get;
        _httpContext.Request.Path = "/api/readings/export";
        _httpContext.Response.Body = _body;

        _sut = new ApiExceptionHandler(_services.GetRequiredService<IProblemDetailsService>(), _logger);
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask DisposeAsync()
    {
        await _services.DisposeAsync();
        await _body.DisposeAsync();
    }

    [Fact]
    public async Task TryHandleAsync_DatabaseException_WritesInternalErrorProblemWithoutConnectionDetails()
    {
        // Act
        var handled = await _sut.TryHandleAsync(_httpContext, new TestDbException("Failed to connect to 10.0.0.5:5432"), Ct);

        // Assert
        handled.ShouldBeTrue();
        _httpContext.Response.StatusCode.ShouldBe(StatusCodes.Status500InternalServerError);

        var problem = ReadProblem();
        problem.GetProperty("status").GetInt32().ShouldBe(StatusCodes.Status500InternalServerError);
        problem.GetProperty("detail").GetString().ShouldBe("An unexpected error occurred.");
        BodyText().ShouldNotContain("10.0.0.5");
    }

    [Fact]
    public async Task TryHandleAsync_UnexpectedException_WritesInternalErrorProblemWithTraceId()
    {
        // Act
        var handled = await _sut.TryHandleAsync(_httpContext, new InvalidOperationException("Secret detail"), Ct);

        // Assert
        handled.ShouldBeTrue();
        _httpContext.Response.StatusCode.ShouldBe(StatusCodes.Status500InternalServerError);
        _httpContext.Response.ContentType.ShouldBe("application/problem+json");

        var problem = ReadProblem();
        problem.GetProperty("status").GetInt32().ShouldBe(StatusCodes.Status500InternalServerError);
        problem.GetProperty("detail").GetString().ShouldBe("An unexpected error occurred.");
        problem.GetProperty("traceId").GetString().ShouldNotBeNullOrWhiteSpace();
        BodyText().ShouldNotContain("Secret detail");
    }

    [Fact]
    public async Task TryHandleAsync_Exception_LogsErrorOnceWithExceptionAndRequest()
    {
        // Arrange
        var exception = new InvalidOperationException("Secret detail");

        // Act
        await _sut.TryHandleAsync(_httpContext, exception, Ct);

        // Assert
        var record = _logger.Collector.GetSnapshot().ShouldHaveSingleItem();
        record.Level.ShouldBe(LogLevel.Error);
        record.Exception.ShouldBeSameAs(exception);
        record.Message.ShouldBe("GET /api/readings/export failed with an unhandled exception");
    }

    private string BodyText()
    {
        return System.Text.Encoding.UTF8.GetString(_body.ToArray());
    }

    private JsonElement ReadProblem()
    {
        return JsonDocument.Parse(_body.ToArray()).RootElement.Clone();
    }
}
