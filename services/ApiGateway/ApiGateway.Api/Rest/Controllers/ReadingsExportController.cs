using System.Globalization;
using ApiGateway.Api.Rest.Csv;
using ApiGateway.Api.Rest.Models;
using ApiGateway.BusinessLogic.MeterReadings;
using ApiGateway.BusinessLogic.MeterReadings.Requests;
using Microsoft.AspNetCore.Mvc;

namespace ApiGateway.Api.Rest.Controllers;

[ApiController]
[Route("api/readings")]
public sealed class ReadingsExportController(IReadingsExportService readingsExportService) : ControllerBase
{
    private const string FileNameTimestampFormat = "yyyyMMdd'T'HHmmss'Z'";

    /// <summary>
    /// Exports readings as a CSV file.
    /// </summary>
    [HttpGet("export")]
    [ProducesResponseType<string>(StatusCodes.Status200OK, ReadingsCsvResult.ContentType)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> ExportReadingsAsync([FromQuery] ExportReadingsQuery query, CancellationToken ct)
    {
        var request = new ExportReadingsRequest
        {
            Types = query.Types,
            Locations = query.Locations,
            From = query.From,
            To = query.To
        };

        var result = await readingsExportService.ExportAsync(request, ct);

        if (!result.TryGetSuccessValue(out var readings))
        {
            return Problem(detail: result.ErrorMessage, statusCode: StatusCodes.Status400BadRequest);
        }

        var fileName = GetFileName(query.From, query.To);

        return new ReadingsCsvResult(readings, fileName);
    }

    private static string GetFileName(DateTimeOffset from, DateTimeOffset to)
    {
        return $"meter-readings_{FormatTimestamp(from)}_{FormatTimestamp(to)}.csv";

        static string FormatTimestamp(DateTimeOffset timestamp)
        {
            return timestamp.UtcDateTime.ToString(FileNameTimestampFormat, CultureInfo.InvariantCulture);
        }
    }
}
