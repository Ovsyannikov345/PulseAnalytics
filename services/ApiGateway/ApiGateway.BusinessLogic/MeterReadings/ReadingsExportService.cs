using ApiGateway.BusinessLogic.MeterReadings.Extensions;
using ApiGateway.BusinessLogic.MeterReadings.Models;
using ApiGateway.BusinessLogic.MeterReadings.Requests;
using ApiGateway.BusinessLogic.Validation;
using ApiGateway.DataAccess.Database;
using ApiGateway.DataAccess.Entities;
using Ausiannikau.Utilities.Results;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace ApiGateway.BusinessLogic.MeterReadings;

public interface IReadingsExportService
{
    /// <summary>
    /// Validates the request and returns the matching readings, oldest first.
    /// </summary>
    public Task<Result<IAsyncEnumerable<ExportReadingModel>>> ExportAsync(
        ExportReadingsRequest request,
        CancellationToken ct);
}

internal sealed class ReadingsExportService(
    IReadOnlyDbContext dbContext,
    IValidator<ExportReadingsRequest> exportReadingsRequestValidator)
    : IReadingsExportService
{
    public async Task<Result<IAsyncEnumerable<ExportReadingModel>>> ExportAsync(
        ExportReadingsRequest request,
        CancellationToken ct)
    {
        var validationResult = await exportReadingsRequestValidator.ValidateAsync(request, ct);

        if (!validationResult.IsValid)
        {
            return validationResult.ToResult().ToFailureOf<IAsyncEnumerable<ExportReadingModel>>();
        }

        var readings = dbContext
            .Query<MeterReading>()
            .ApplyFilter(request)
            .ApplySort(request)
            .Select(r => new ExportReadingModel
            {
                Id = r.Id,
                Type = r.Type,
                Location = r.Location,
                ReceivedAt = r.ReceivedAt,
                MotionDetected = r.MotionDetected,
                Energy = r.Energy,
                Co2 = r.Co2,
                Pm25 = r.Pm25,
                Humidity = r.Humidity
            })
            .AsAsyncEnumerable();

        return Result<IAsyncEnumerable<ExportReadingModel>>.Succeed(readings);
    }
}
