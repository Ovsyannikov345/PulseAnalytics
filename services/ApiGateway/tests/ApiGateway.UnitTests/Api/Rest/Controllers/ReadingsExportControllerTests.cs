using ApiGateway.Api.Rest.Controllers;
using ApiGateway.Api.Rest.Csv;
using ApiGateway.Api.Rest.Models;
using ApiGateway.BusinessLogic.MeterReadings;
using ApiGateway.BusinessLogic.MeterReadings.Models;
using ApiGateway.BusinessLogic.MeterReadings.Requests;
using ApiGateway.Common.Constants.Enums;
using Ausiannikau.Utilities.Results;
using FakeItEasy;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace ApiGateway.UnitTests.Api.Rest.Controllers;

public sealed class ReadingsExportControllerTests : IAsyncDisposable
{
    private static readonly DateTimeOffset From = new(2026, 11, 4, 14, 0, 0, TimeSpan.FromHours(2));

    private readonly IReadingsExportService _readingsExportService = A.Fake<IReadingsExportService>();
    private readonly IAsyncEnumerable<ExportReadingModel> _readings = AsyncEnumerable.Empty<ExportReadingModel>();
    private readonly ServiceProvider _services;
    private readonly ReadingsExportController _sut;

    public ReadingsExportControllerTests()
    {
        // The real ProblemDetailsFactory, as registered by AddControllers.
        _services = new ServiceCollection()
            .AddLogging()
            .AddControllers()
            .Services
            .BuildServiceProvider();

        A.CallTo(() => _readingsExportService.ExportAsync(A<ExportReadingsRequest>._, A<CancellationToken>._))
            .Returns(Result<IAsyncEnumerable<ExportReadingModel>>.Succeed(_readings));

        _sut = new ReadingsExportController(_readingsExportService)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { RequestServices = _services }
            }
        };
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask DisposeAsync()
    {
        await _services.DisposeAsync();
    }

    [Fact]
    public async Task ExportReadingsAsync_Query_PassesItsValuesToTheService()
    {
        // Arrange
        ExportReadingsRequest? request = null;
        A.CallTo(() => _readingsExportService.ExportAsync(A<ExportReadingsRequest>._, Ct))
            .Invokes((ExportReadingsRequest r, CancellationToken _) => request = r)
            .Returns(Result<IAsyncEnumerable<ExportReadingModel>>.Succeed(_readings));

        var query = Query(From.AddDays(1)) with
        {
            Types = [MeterType.Energy, MeterType.Motion],
            Locations = [MeterLocation.Kitchen]
        };

        // Act
        await _sut.ExportReadingsAsync(query, Ct);

        // Assert
        request.ShouldNotBeNull();
        request.From.ShouldBe(From);
        request.To.ShouldBe(From.AddDays(1));
        request.Types.ShouldBe([MeterType.Energy, MeterType.Motion]);
        request.Locations.ShouldBe([MeterLocation.Kitchen]);
    }

    [Fact]
    public async Task ExportReadingsAsync_NoTypesOrLocations_PassesNullFilters()
    {
        // Arrange
        ExportReadingsRequest? request = null;
        A.CallTo(() => _readingsExportService.ExportAsync(A<ExportReadingsRequest>._, Ct))
            .Invokes((ExportReadingsRequest r, CancellationToken _) => request = r)
            .Returns(Result<IAsyncEnumerable<ExportReadingModel>>.Succeed(_readings));

        // Act
        await _sut.ExportReadingsAsync(Query(From.AddDays(1)), Ct);

        // Assert
        request.ShouldNotBeNull();
        request.Types.ShouldBeNull();
        request.Locations.ShouldBeNull();
    }

    [Fact]
    public async Task ExportReadingsAsync_ValidRequest_ReturnsCsvResultWithUtcRangeInFileName()
    {
        // Act
        var result = await _sut.ExportReadingsAsync(Query(From.AddDays(1).AddMinutes(30)), Ct);

        // Assert
        var csvResult = result.ShouldBeOfType<ReadingsCsvResult>();
        csvResult.Readings.ShouldBeSameAs(_readings);
        csvResult.FileName.ShouldBe("meter-readings_20261104T120000Z_20261105T123000Z.csv");
    }

    [Fact]
    public async Task ExportReadingsAsync_InvalidRequest_ReturnsBadRequestProblemWithMessage()
    {
        // Arrange
        A.CallTo(() => _readingsExportService.ExportAsync(A<ExportReadingsRequest>._, A<CancellationToken>._))
            .Returns(Result.Fail("'To' must be at most 7 days after 'From'.").ToFailureOf<IAsyncEnumerable<ExportReadingModel>>());

        // Act
        var result = await _sut.ExportReadingsAsync(Query(From.AddDays(8)), Ct);

        // Assert
        var objectResult = result.ShouldBeOfType<ObjectResult>();
        objectResult.StatusCode.ShouldBe(StatusCodes.Status400BadRequest);

        var problem = objectResult.Value.ShouldBeOfType<ProblemDetails>();
        problem.Status.ShouldBe(StatusCodes.Status400BadRequest);
        problem.Detail.ShouldBe("'To' must be at most 7 days after 'From'.");
    }

    private static ExportReadingsQuery Query(DateTimeOffset to)
    {
        return new ExportReadingsQuery
        {
            From = From,
            To = to
        };
    }
}
