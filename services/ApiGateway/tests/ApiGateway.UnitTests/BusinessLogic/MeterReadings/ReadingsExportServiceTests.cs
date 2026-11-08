using ApiGateway.BusinessLogic.MeterReadings;
using ApiGateway.BusinessLogic.MeterReadings.Models;
using ApiGateway.BusinessLogic.MeterReadings.Requests;
using ApiGateway.BusinessLogic.MeterReadings.Validators;
using ApiGateway.Common.Constants.Enums;
using ApiGateway.DataAccess.Database;
using ApiGateway.DataAccess.Entities;
using ApiGateway.UnitTests.TestDoubles;
using ApiGateway.UnitTests.Utilities;
using FakeItEasy;
using Shouldly;
using Xunit;

namespace ApiGateway.UnitTests.BusinessLogic.MeterReadings;

// The real validator is used: it is pure, and the service's contract includes rejecting what it rejects.
// The query runs in memory, so these tests check its logic; the SQL is checked by the translation tests
// and the integration tests.
public sealed class ReadingsExportServiceTests
{
    private static readonly DateTimeOffset Start = new(2026, 11, 4, 12, 0, 0, TimeSpan.Zero);

    private readonly IReadOnlyDbContext _dbContext = A.Fake<IReadOnlyDbContext>();
    private readonly List<MeterReading> _storedReadings = [];
    private readonly ReadingsExportService _sut;

    public ReadingsExportServiceTests()
    {
        A.CallTo(() => _dbContext.Query<MeterReading>())
            .ReturnsLazily(() => _storedReadings.AsAsyncQueryable());

        _sut = new ReadingsExportService(_dbContext, new ExportReadingsRequestValidator());
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task ExportAsync_ReadingOfEachType_ReturnsFlatRowsWithOnlyTheTypesOwnValues()
    {
        // Arrange
        var motion = StoredReadings.Motion(MeterLocation.Garage, Start, motionDetected: true);
        var energy = StoredReadings.Energy(MeterLocation.Kitchen, Start.AddSeconds(1), energy: 2.25);
        var airQuality = StoredReadings.AirQuality(MeterLocation.Office, Start.AddSeconds(2), co2: 800, pm25: 15, humidity: 40);
        _storedReadings.AddRange([motion, energy, airQuality]);

        // Act
        var readings = await StreamAsync(Request());

        // Assert
        readings.ShouldBe(
        [
            new ExportReadingModel
            {
                Id = motion.Id,
                Type = MeterType.Motion,
                Location = MeterLocation.Garage,
                ReceivedAt = Start,
                MotionDetected = true
            },
            new ExportReadingModel
            {
                Id = energy.Id,
                Type = MeterType.Energy,
                Location = MeterLocation.Kitchen,
                ReceivedAt = Start.AddSeconds(1),
                Energy = 2.25
            },
            new ExportReadingModel
            {
                Id = airQuality.Id,
                Type = MeterType.AirQuality,
                Location = MeterLocation.Office,
                ReceivedAt = Start.AddSeconds(2),
                Co2 = 800,
                Pm25 = 15,
                Humidity = 40
            }
        ]);
    }

    [Fact]
    public async Task ExportAsync_ReadingsOutOfOrder_ReturnsOldestFirstWithIdBreakingTies()
    {
        // Arrange
        var firstId = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var secondId = Guid.Parse("00000000-0000-0000-0000-000000000002");
        _storedReadings.AddRange(
        [
            StoredReadings.Energy(MeterLocation.Kitchen, Start.AddMinutes(1), id: Guid.Parse("00000000-0000-0000-0000-000000000003")),
            StoredReadings.Energy(MeterLocation.Kitchen, Start, id: secondId),
            StoredReadings.Motion(MeterLocation.Kitchen, Start, id: firstId)
        ]);

        // Act
        var readings = await StreamAsync(Request());

        // Assert
        readings.Select(r => r.Id).ShouldBe([firstId, secondId, Guid.Parse("00000000-0000-0000-0000-000000000003")]);
    }

    [Fact]
    public async Task ExportAsync_TypesAndLocations_ReturnsOnlyMatchingReadings()
    {
        // Arrange
        var match = StoredReadings.Energy(MeterLocation.Kitchen, Start);
        _storedReadings.AddRange(
        [
            match,
            StoredReadings.Energy(MeterLocation.Garage, Start),
            StoredReadings.Motion(MeterLocation.Kitchen, Start)
        ]);

        // Act
        var readings = await StreamAsync(Request() with { Types = [MeterType.Energy], Locations = [MeterLocation.Kitchen] });

        // Assert
        readings.ShouldHaveSingleItem().Id.ShouldBe(match.Id);
    }

    [Fact]
    public async Task ExportAsync_ReadingsAtRangeEdges_IncludesFromAndExcludesTo()
    {
        // Arrange
        var atFrom = StoredReadings.Energy(MeterLocation.Kitchen, Start);
        var beforeTo = StoredReadings.Energy(MeterLocation.Kitchen, Start.AddHours(1).AddTicks(-1));
        _storedReadings.AddRange(
        [
            StoredReadings.Energy(MeterLocation.Kitchen, Start.AddTicks(-1)),
            atFrom,
            beforeTo,
            StoredReadings.Energy(MeterLocation.Kitchen, Start.AddHours(1))
        ]);

        // Act
        var readings = await StreamAsync(Request() with { To = Start.AddHours(1) });

        // Assert
        readings.Select(r => r.Id).ShouldBe([atFrom.Id, beforeTo.Id]);
    }

    [Fact]
    public async Task ExportAsync_RangeWithOffset_FiltersByInstant()
    {
        // Arrange
        var inRange = StoredReadings.Energy(MeterLocation.Kitchen, Start);
        _storedReadings.AddRange([StoredReadings.Energy(MeterLocation.Kitchen, Start.AddHours(-1)), inRange]);
        var offset = TimeSpan.FromHours(2);

        // Act
        // 14:00+02:00 is 12:00 UTC.
        var readings = await StreamAsync(Request() with { From = Start.ToOffset(offset), To = Start.AddHours(1).ToOffset(offset) });

        // Assert
        readings.ShouldHaveSingleItem().Id.ShouldBe(inRange.Id);
    }

    [Fact]
    public async Task ExportAsync_NoMatchingReadings_ReturnsEmptySequence()
    {
        // Act
        var readings = await StreamAsync(Request());

        // Assert
        readings.ShouldBeEmpty();
    }

    [Fact]
    public async Task ExportAsync_InvalidRequest_ReturnsFailureWithoutQuerying()
    {
        // Act
        var result = await _sut.ExportAsync(Request() with { To = Start.AddDays(8) }, Ct);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.ErrorMessage.ShouldBe("'To' must be at most 7 days after 'From'.");
        A.CallTo(() => _dbContext.Query<MeterReading>()).MustNotHaveHappened();
    }

    private static ExportReadingsRequest Request()
    {
        return new ExportReadingsRequest
        {
            From = Start,
            To = Start.AddDays(1)
        };
    }

    private async Task<List<ExportReadingModel>> StreamAsync(ExportReadingsRequest request)
    {
        var result = await _sut.ExportAsync(request, Ct);

        return await result.Value.ToListAsync(Ct);
    }
}
