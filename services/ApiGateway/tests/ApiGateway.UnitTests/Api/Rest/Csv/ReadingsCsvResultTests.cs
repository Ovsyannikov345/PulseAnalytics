using System.Globalization;
using System.Text;
using ApiGateway.Api.Rest.Csv;
using ApiGateway.BusinessLogic.MeterReadings.Models;
using ApiGateway.Common.Constants.Enums;
using ApiGateway.UnitTests.TestDoubles;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using Shouldly;
using Xunit;

namespace ApiGateway.UnitTests.Api.Rest.Csv;

public sealed class ReadingsCsvResultTests : IAsyncDisposable
{
    private const string FileName = "meter-readings_20261104T120000Z_20261105T120000Z.csv";

    private const string Header = "id,type,location,received_at,motion_detected,energy,co2,pm25,humidity";

    private static readonly DateTimeOffset Start = new(2026, 11, 4, 12, 0, 0, TimeSpan.Zero);

    private readonly FakeLogger<ReadingsCsvResult> _logger = new();
    private readonly MemoryStream _body = new();
    private readonly TestLifetimeFeature _lifetime = new();
    private readonly ServiceProvider _services;
    private readonly DefaultHttpContext _httpContext;

    public ReadingsCsvResultTests()
    {
        _services = new ServiceCollection()
            .AddSingleton<ILogger<ReadingsCsvResult>>(_logger)
            .BuildServiceProvider();

        _httpContext = new DefaultHttpContext { RequestServices = _services };
        _httpContext.Response.Body = _body;
        _httpContext.Features.Set<IHttpResponseFeature>(new TestResponseFeature(_body));
        _httpContext.Features.Set<IHttpRequestLifetimeFeature>(_lifetime);
    }

    public async ValueTask DisposeAsync()
    {
        await _services.DisposeAsync();
        await _body.DisposeAsync();
    }

    [Fact]
    public async Task ExecuteResultAsync_Readings_WritesCsvDownload()
    {
        // Act
        await ExecuteAsync(Readings(2));

        // Assert
        _httpContext.Response.StatusCode.ShouldBe(StatusCodes.Status200OK);
        _httpContext.Response.ContentType.ShouldBe("text/csv; charset=utf-8");
        _httpContext.Response.Headers.ContentDisposition.ToString().ShouldBe($"attachment; filename={FileName}; filename*=UTF-8''{FileName}");

        var lines = BodyLines();
        lines.ShouldBe(
        [
            Header,
            $"{RowId(0)},Energy,Kitchen,2026-11-04T12:00:00.0000000Z,,0,,,",
            $"{RowId(1)},Energy,Kitchen,2026-11-04T12:00:01.0000000Z,,1,,,"
        ]);
    }

    [Fact]
    public async Task ExecuteResultAsync_NoReadings_WritesHeaderOnly()
    {
        // Act
        await ExecuteAsync(Readings(0));

        // Assert
        _httpContext.Response.StatusCode.ShouldBe(StatusCodes.Status200OK);
        BodyLines().ShouldBe([Header]);
    }

    [Fact]
    public async Task ExecuteResultAsync_MoreRowsThanFlushInterval_WritesEveryRow()
    {
        // Arrange
        const int rowCount = (ReadingsCsvResult.RowsPerFlush * 2) + 1;

        // Act
        await ExecuteAsync(Readings(rowCount));

        // Assert
        var lines = BodyLines();
        lines.Length.ShouldBe(rowCount + 1);
        lines[^1].ShouldStartWith(RowId(rowCount - 1).ToString());
    }

    [Fact]
    public async Task ExecuteResultAsync_FirstReadFails_RethrowsBeforeWritingAnything()
    {
        // Arrange
        var exception = new TestDbException("Failed to connect");

        // Act
        var thrown = await Should.ThrowAsync<TestDbException>(() => ExecuteAsync(Readings(0, thenThrow: exception)));

        // Assert
        thrown.ShouldBeSameAs(exception);
        _body.Length.ShouldBe(0);
        _httpContext.Response.Headers.ContentDisposition.ShouldBeEmpty();
        _lifetime.AbortCalled.ShouldBeFalse();
        _logger.Collector.Count.ShouldBe(0);
    }

    [Fact]
    public async Task ExecuteResultAsync_ReadFailsAfterResponseStarted_AbortsAndLogsOnce()
    {
        // Arrange
        const int rowCount = ReadingsCsvResult.RowsPerFlush + 100;
        var exception = new TestDbException("Connection reset");

        // Act
        await ExecuteAsync(Readings(rowCount, thenThrow: exception));

        // Assert
        _lifetime.AbortCalled.ShouldBeTrue();

        var record = _logger.Collector.GetSnapshot().ShouldHaveSingleItem();
        record.Level.ShouldBe(LogLevel.Error);
        record.Exception.ShouldBeSameAs(exception);
        record.Message.ShouldContain(rowCount.ToString(CultureInfo.InvariantCulture));

        // Only the flushed rows reached the client.
        BodyLines().Length.ShouldBe(ReadingsCsvResult.RowsPerFlush + 1);
    }

    [Fact]
    public async Task ExecuteResultAsync_ClientDisconnects_StopsWithoutLoggingOrAborting()
    {
        // Arrange
        using var requestAborted = new CancellationTokenSource();
        _lifetime.RequestAborted = requestAborted.Token;

        // Act
        await ExecuteAsync(Readings(ReadingsCsvResult.RowsPerFlush * 3, cancelAfter: ReadingsCsvResult.RowsPerFlush + 1, requestAborted));

        // Assert
        _lifetime.AbortCalled.ShouldBeFalse();
        _logger.Collector.Count.ShouldBe(0);
        BodyLines().Length.ShouldBe(ReadingsCsvResult.RowsPerFlush + 1);
    }

    private static Guid RowId(int index)
    {
        return new Guid(index, 0, 0, [0, 0, 0, 0, 0, 0, 0, 1]);
    }

    private static async IAsyncEnumerable<ExportReadingModel> Readings(int count, Exception? thenThrow = null)
    {
        for (var index = 0; index < count; index++)
        {
            await Task.Yield();

            yield return Row(index);
        }

        if (thenThrow is not null)
        {
            throw thenThrow;
        }
    }

    // Cancels the request (as Kestrel does when the client disconnects) once the given number of rows was read,
    // then fails the next read the way a cancelled database read does.
    private static async IAsyncEnumerable<ExportReadingModel> Readings(int count, int cancelAfter, CancellationTokenSource requestAborted)
    {
        for (var index = 0; index < count; index++)
        {
            if (index == cancelAfter)
            {
                await requestAborted.CancelAsync();
            }

            requestAborted.Token.ThrowIfCancellationRequested();

            yield return Row(index);
        }
    }

    private static ExportReadingModel Row(int index)
    {
        return new ExportReadingModel
        {
            Id = RowId(index),
            Type = MeterType.Energy,
            Location = MeterLocation.Kitchen,
            ReceivedAt = Start.AddSeconds(index),
            Energy = index
        };
    }

    private async Task ExecuteAsync(IAsyncEnumerable<ExportReadingModel> readings)
    {
        var sut = new ReadingsCsvResult(readings, FileName);

        await sut.ExecuteResultAsync(new ActionContext(_httpContext, new RouteData(), new ActionDescriptor()));
    }

    private string[] BodyLines()
    {
        var body = Encoding.UTF8.GetString(_body.ToArray());

        body.Length.ShouldBeGreaterThan(0);
        body.ShouldEndWith("\r\n");

        return body[..^2].Split("\r\n");
    }

    /// <summary>
    /// Reports the response as started once bytes reached the body, as a server does after the first flush.
    /// </summary>
    private sealed class TestResponseFeature(MemoryStream body) : IHttpResponseFeature
    {
        public int StatusCode { get; set; } = StatusCodes.Status200OK;

        public string? ReasonPhrase { get; set; }

        public IHeaderDictionary Headers { get; set; } = new HeaderDictionary();

        public Stream Body { get; set; } = body;

        public bool HasStarted => body.Length > 0;

        public void OnStarting(Func<object, Task> callback, object state)
        {
        }

        public void OnCompleted(Func<object, Task> callback, object state)
        {
        }
    }

    private sealed class TestLifetimeFeature : IHttpRequestLifetimeFeature
    {
        public CancellationToken RequestAborted { get; set; }

        public bool AbortCalled { get; private set; }

        public void Abort()
        {
            AbortCalled = true;
        }
    }
}
