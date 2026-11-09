using ApiGateway.BusinessLogic.MeterReadings.Models;
using ApiGateway.BusinessLogic.Telemetry.Metrics;
using ApiGateway.BusinessLogic.Telemetry.Metrics.Enums;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;

namespace ApiGateway.Api.Rest.Csv;

/// <summary>
/// Streams readings to the client as a CSV file download while they are read from the database.
/// </summary>
internal sealed class ReadingsCsvResult(IAsyncEnumerable<ExportReadingModel> readings, string fileName) : IActionResult
{
    /// <summary>
    /// Rows written between flushes, which bounds the response buffer.
    /// </summary>
    public const int RowsPerFlush = 500;

    public const string ContentType = "text/csv; charset=utf-8";

    public IAsyncEnumerable<ExportReadingModel> Readings => readings;

    public string FileName => fileName;

    public async Task ExecuteResultAsync(ActionContext context)
    {
        var httpContext = context.HttpContext;
        var response = httpContext.Response;
        var ct = httpContext.RequestAborted;
        var rowsWritten = 0;
        var outcome = ExportOutcome.Failed;

        await using var enumerator = readings.GetAsyncEnumerator(ct);

        try
        {
            var hasRow = await enumerator.MoveNextAsync();

            var contentDisposition = new ContentDispositionHeaderValue("attachment");
            contentDisposition.SetHttpFileName(fileName);

            response.StatusCode = StatusCodes.Status200OK;
            response.ContentType = ContentType;
            response.Headers.ContentDisposition = contentDisposition.ToString();

            var writer = response.BodyWriter;

            ReadingsCsvWriter.WriteHeader(writer);

            while (hasRow)
            {
                ReadingsCsvWriter.WriteRow(writer, enumerator.Current);
                rowsWritten++;

                if (rowsWritten % RowsPerFlush == 0)
                {
                    var flushResult = await writer.FlushAsync(ct);

                    if (flushResult.IsCompleted || flushResult.IsCanceled)
                    {
                        outcome = ExportOutcome.Cancelled;
                        GetLogger(httpContext).ClientDisconnected(rowsWritten);

                        return;
                    }
                }

                hasRow = await enumerator.MoveNextAsync();
            }

            await writer.FlushAsync(ct);

            outcome = ExportOutcome.Completed;
        }
        catch (Exception exception) when (exception is OperationCanceledException or IOException && ct.IsCancellationRequested)
        {
            outcome = ExportOutcome.Cancelled;
            GetLogger(httpContext).ClientDisconnected(rowsWritten);
        }
        catch (Exception exception) when (response.HasStarted)
        {
            GetLogger(httpContext).ExportAborted(exception, rowsWritten);

            httpContext.Abort();
        }
        finally
        {
            GetMetrics(httpContext).RecordExport(rowsWritten, outcome);
        }
    }

    private static ILogger<ReadingsCsvResult> GetLogger(HttpContext httpContext)
    {
        return httpContext.RequestServices.GetRequiredService<ILogger<ReadingsCsvResult>>();
    }

    private static IMetricsService GetMetrics(HttpContext httpContext)
    {
        return httpContext.RequestServices.GetRequiredService<IMetricsService>();
    }
}

internal static partial class ReadingsCsvResultLogs
{
    [LoggerMessage(Level = LogLevel.Debug, Message = "CSV export stopped after {RowsWritten} rows, the client disconnected")]
    public static partial void ClientDisconnected(this ILogger logger, int rowsWritten);


    [LoggerMessage(Level = LogLevel.Error, Message = "CSV export failed after {RowsWritten} rows, the response was aborted")]
    public static partial void ExportAborted(this ILogger logger, Exception exception, int rowsWritten);
}
