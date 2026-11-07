using ApiGateway.BusinessLogic.MeterReadings;
using ApiGateway.BusinessLogic.MeterReadings.Models;
using ApiGateway.BusinessLogic.MeterReadings.Requests;
using ApiGateway.BusinessLogic.MeterReadings.Validators;
using ApiGateway.BusinessLogic.Paging;
using ApiGateway.Common.Constants.Enums;
using ApiGateway.DataAccess.Database;
using ApiGateway.DataAccess.Entities;
using ApiGateway.UnitTests.TestDoubles;
using ApiGateway.UnitTests.Utilities;
using FakeItEasy;
using Shouldly;
using Xunit;

namespace ApiGateway.UnitTests.BusinessLogic.MeterReadings;

// The real validators are used: they are pure, and the service's contract includes rejecting what they reject.
// The queries run in memory, so these tests check the query logic; the SQL is checked by the integration tests.
public sealed class MeterReadingsServiceTests
{
    private static readonly DateTimeOffset Start = new(2026, 11, 4, 12, 0, 0, TimeSpan.Zero);

    private readonly IReadOnlyDbContext _dbContext = A.Fake<IReadOnlyDbContext>();
    private readonly List<MeterReading> _storedReadings = [];
    private readonly MeterReadingsService _sut;

    public MeterReadingsServiceTests()
    {
        A.CallTo(() => _dbContext.Query<MeterReading>())
            .ReturnsLazily(() => _storedReadings.AsAsyncQueryable());

        _sut = new MeterReadingsService(
            _dbContext,
            new GetAllReadingsRequestValidator(),
            new GetLatestReadingsRequestValidator(),
            new GetReadingCountsRequestValidator());
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task GetAllReadingsAsync_NoPageSize_ReturnsDefaultPageNewestFirst()
    {
        // Arrange
        _storedReadings.AddRange(Enumerable.Range(0, 60).Select(i => StoredReadings.Energy(MeterLocation.Kitchen, Start.AddSeconds(i))));

        // Act
        var result = await _sut.GetAllReadingsAsync(new GetAllReadingsRequest(), Ct);

        // Assert
        var page = result.Value;
        page.Edges.Count.ShouldBe(PagingLimits.DefaultPageSize);
        page.Edges.Select(e => e.Node.ReceivedAt).ShouldBe(Enumerable.Range(10, 50).Reverse().Select(i => Start.AddSeconds(i)));
        page.HasNextPage.ShouldBeTrue();
        page.HasPreviousPage.ShouldBeFalse();
    }

    [Fact]
    public async Task GetAllReadingsAsync_LastPage_HasNoNextPage()
    {
        // Arrange
        _storedReadings.AddRange(Enumerable.Range(0, 3).Select(i => StoredReadings.Energy(MeterLocation.Kitchen, Start.AddSeconds(i))));

        // Act
        var result = await _sut.GetAllReadingsAsync(new GetAllReadingsRequest { PageSize = 3 }, Ct);

        // Assert
        result.Value.Edges.Count.ShouldBe(3);
        result.Value.HasNextPage.ShouldBeFalse();
    }

    [Fact]
    public async Task GetAllReadingsAsync_FollowingCursors_ReturnsEveryReadingOnceInOrder()
    {
        // Arrange
        // Two polls of three readings each: readings of one poll share ReceivedAt, so the id breaks the tie.
        _storedReadings.AddRange(
        [
            StoredReadings.Motion(MeterLocation.Garage, Start, id: IdEndingWith(1)),
            StoredReadings.Energy(MeterLocation.Garage, Start, id: IdEndingWith(2)),
            StoredReadings.AirQuality(MeterLocation.Garage, Start, id: IdEndingWith(3)),
            StoredReadings.Motion(MeterLocation.Office, Start.AddSeconds(10), id: IdEndingWith(4)),
            StoredReadings.Energy(MeterLocation.Office, Start.AddSeconds(10), id: IdEndingWith(5)),
            StoredReadings.AirQuality(MeterLocation.Office, Start.AddSeconds(10), id: IdEndingWith(6))
        ]);

        var pages = new List<CursorPage<MeterReadingModel>>();
        string? cursor = null;

        // Act
        do
        {
            var result = await _sut.GetAllReadingsAsync(new GetAllReadingsRequest { PageSize = 2, Cursor = cursor }, Ct);

            pages.Add(result.Value);
            cursor = result.Value.Edges[^1].Cursor;
        }
        while (pages[^1].HasNextPage);

        // Assert
        pages.Count.ShouldBe(3);
        pages.SelectMany(p => p.Edges).Select(e => e.Node.Id).ShouldBe([.. new[] { 6, 5, 4, 3, 2, 1 }.Select(IdEndingWith)]);
        pages.Skip(1).ShouldAllBe(p => p.HasPreviousPage);
    }

    [Fact]
    public async Task GetAllReadingsAsync_EdgeCursor_PointsToItsReading()
    {
        // Arrange
        var stored = StoredReadings.Energy(MeterLocation.Bedroom, Start);
        _storedReadings.Add(stored);

        // Act
        var result = await _sut.GetAllReadingsAsync(new GetAllReadingsRequest(), Ct);

        // Assert
        var cursor = ReadingCursor.Decode(result.Value.Edges.ShouldHaveSingleItem().Cursor).Value;
        cursor.ShouldBe(new ReadingCursor(stored.ReceivedAt, stored.Id));
    }

    [Fact]
    public async Task GetAllReadingsAsync_TypesAndLocations_ReturnsMatchingReadings()
    {
        // Arrange
        var kitchenEnergy = StoredReadings.Energy(MeterLocation.Kitchen, Start);
        var officeMotion = StoredReadings.Motion(MeterLocation.Office, Start);
        _storedReadings.AddRange(
        [
            kitchenEnergy,
            officeMotion,
            StoredReadings.AirQuality(MeterLocation.Kitchen, Start),
            StoredReadings.Energy(MeterLocation.Garage, Start)
        ]);

        var request = new GetAllReadingsRequest
        {
            Types = [MeterType.Energy, MeterType.Motion],
            Locations = [MeterLocation.Kitchen, MeterLocation.Office]
        };

        // Act
        var result = await _sut.GetAllReadingsAsync(request, Ct);

        // Assert
        result.Value.Edges.Select(e => e.Node.Id).ShouldBe([kitchenEnergy.Id, officeMotion.Id], ignoreOrder: true);
    }

    [Fact]
    public async Task GetAllReadingsAsync_TimeRange_IncludesFromAndExcludesTo()
    {
        // Arrange
        var to = Start.AddMinutes(1);
        var atFrom = StoredReadings.Energy(MeterLocation.Kitchen, Start);
        var beforeTo = StoredReadings.Energy(MeterLocation.Kitchen, to.AddTicks(-10));
        _storedReadings.AddRange(
        [
            StoredReadings.Energy(MeterLocation.Kitchen, Start.AddTicks(-10)),
            atFrom,
            beforeTo,
            StoredReadings.Energy(MeterLocation.Kitchen, to)
        ]);

        var request = new GetAllReadingsRequest { From = Start, To = to };

        // Act
        var result = await _sut.GetAllReadingsAsync(request, Ct);

        // Assert
        result.Value.Edges.Select(e => e.Node.Id).ShouldBe([beforeTo.Id, atFrom.Id]);
    }

    [Fact]
    public async Task GetAllReadingsAsync_TimeRangeWithOffset_ComparesInstants()
    {
        // Arrange
        var inRange = StoredReadings.Energy(MeterLocation.Kitchen, Start);
        _storedReadings.AddRange([inRange, StoredReadings.Energy(MeterLocation.Kitchen, Start.AddHours(-1))]);

        var plusTwo = TimeSpan.FromHours(2);
        var request = new GetAllReadingsRequest { From = Start.ToOffset(plusTwo), To = Start.AddMinutes(1).ToOffset(plusTwo) };

        // Act
        var result = await _sut.GetAllReadingsAsync(request, Ct);

        // Assert
        result.Value.Edges.ShouldHaveSingleItem().Node.Id.ShouldBe(inRange.Id);
    }

    [Fact]
    public async Task GetAllReadingsAsync_InvalidFilter_FailsWithoutQuerying()
    {
        // Arrange
        var request = new GetAllReadingsRequest { From = Start, To = Start };

        // Act
        var result = await _sut.GetAllReadingsAsync(request, Ct);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.ErrorMessage.ShouldBe("'From' must be earlier than 'To'.");
        A.CallTo(() => _dbContext.Query<MeterReading>()).MustNotHaveHappened();
    }

    [Fact]
    public async Task GetAllReadingsAsync_PageSizeOutOfRange_FailsWithoutQuerying()
    {
        // Act
        var result = await _sut.GetAllReadingsAsync(new GetAllReadingsRequest { PageSize = 0 }, Ct);

        // Assert
        result.IsFailure.ShouldBeTrue();
        A.CallTo(() => _dbContext.Query<MeterReading>()).MustNotHaveHappened();
    }

    [Fact]
    public async Task GetAllReadingsAsync_InvalidCursor_Fails()
    {
        // Act
        var result = await _sut.GetAllReadingsAsync(new GetAllReadingsRequest { Cursor = "garbage" }, Ct);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.ErrorMessage.ShouldBe("'Cursor' is invalid.");
    }

    [Fact]
    public async Task GetLatestReadingsAsync_SeveralReadingsPerMeter_ReturnsNewestPerMeterOrderedByTypeAndLocation()
    {
        // Arrange
        var newestOfficeMotion = StoredReadings.Motion(MeterLocation.Office, Start.AddSeconds(20));
        var newestKitchenMotion = StoredReadings.Motion(MeterLocation.Kitchen, Start.AddSeconds(10));
        var newestGarageEnergy = StoredReadings.Energy(MeterLocation.Garage, Start.AddSeconds(30));
        var newestKitchenAirQuality = StoredReadings.AirQuality(MeterLocation.Kitchen, Start);
        _storedReadings.AddRange(
        [
            StoredReadings.Motion(MeterLocation.Office, Start),
            newestOfficeMotion,
            newestKitchenMotion,
            StoredReadings.Energy(MeterLocation.Garage, Start),
            newestGarageEnergy,
            newestKitchenAirQuality
        ]);

        // Act
        var result = await _sut.GetLatestReadingsAsync(new GetLatestReadingsRequest(), Ct);

        // Assert
        result.Value.Select(r => r.Id).ShouldBe(
        [
            newestKitchenAirQuality.Id,
            newestGarageEnergy.Id,
            newestKitchenMotion.Id,
            newestOfficeMotion.Id
        ]);
    }

    [Fact]
    public async Task GetLatestReadingsAsync_SameReceivedAt_ReturnsHighestId()
    {
        // Arrange
        _storedReadings.AddRange(
        [
            StoredReadings.Energy(MeterLocation.Garage, Start, id: IdEndingWith(1)),
            StoredReadings.Energy(MeterLocation.Garage, Start, id: IdEndingWith(2))
        ]);

        // Act
        var result = await _sut.GetLatestReadingsAsync(new GetLatestReadingsRequest(), Ct);

        // Assert
        result.Value.ShouldHaveSingleItem().Id.ShouldBe(IdEndingWith(2));
    }

    [Fact]
    public async Task GetLatestReadingsAsync_Filter_ReturnsSelectedMetersOnce()
    {
        // Arrange
        var kitchenEnergy = StoredReadings.Energy(MeterLocation.Kitchen, Start);
        _storedReadings.AddRange(
        [
            kitchenEnergy,
            StoredReadings.Energy(MeterLocation.Garage, Start),
            StoredReadings.Motion(MeterLocation.Kitchen, Start)
        ]);

        var request = new GetLatestReadingsRequest
        {
            Types = [MeterType.Energy, MeterType.Energy],
            Locations = [MeterLocation.Kitchen]
        };

        // Act
        var result = await _sut.GetLatestReadingsAsync(request, Ct);

        // Assert
        result.Value.ShouldHaveSingleItem().Id.ShouldBe(kitchenEnergy.Id);
    }

    [Fact]
    public async Task GetLatestReadingsAsync_NoReadings_ReturnsEmptyList()
    {
        // Act
        var result = await _sut.GetLatestReadingsAsync(new GetLatestReadingsRequest(), Ct);

        // Assert
        result.Value.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetLatestReadingsAsync_InvalidFilter_FailsWithoutQuerying()
    {
        // Arrange
        var request = new GetLatestReadingsRequest { Types = [(MeterType)99] };

        // Act
        var result = await _sut.GetLatestReadingsAsync(request, Ct);

        // Assert
        result.IsFailure.ShouldBeTrue();
        A.CallTo(() => _dbContext.Query<MeterReading>()).MustNotHaveHappened();
    }

    [Fact]
    public async Task GetReadingCountsAsync_ByTypeAndLocation_CountsEachMeterOrderedByTypeAndLocation()
    {
        // Arrange
        StoreCountsSample();

        // Act
        var result = await _sut.GetReadingCountsAsync(
            new GetReadingCountsRequest { GroupBy = [ReadingGrouping.Type, ReadingGrouping.Location] },
            Ct);

        // Assert
        result.Value.ShouldBe(
        [
            new ReadingCountModel { Type = MeterType.AirQuality, Location = MeterLocation.Bedroom, Count = 1 },
            new ReadingCountModel { Type = MeterType.Energy, Location = MeterLocation.Kitchen, Count = 2 },
            new ReadingCountModel { Type = MeterType.Energy, Location = MeterLocation.Garage, Count = 1 },
            new ReadingCountModel { Type = MeterType.Motion, Location = MeterLocation.Kitchen, Count = 3 }
        ]);
    }

    [Fact]
    public async Task GetReadingCountsAsync_ByType_CountsEachTypeWithoutLocation()
    {
        // Arrange
        StoreCountsSample();

        // Act
        var result = await _sut.GetReadingCountsAsync(new GetReadingCountsRequest { GroupBy = [ReadingGrouping.Type] }, Ct);

        // Assert
        result.Value.ShouldBe(
        [
            new ReadingCountModel { Type = MeterType.AirQuality, Count = 1 },
            new ReadingCountModel { Type = MeterType.Energy, Count = 3 },
            new ReadingCountModel { Type = MeterType.Motion, Count = 3 }
        ]);
    }

    [Fact]
    public async Task GetReadingCountsAsync_ByLocation_CountsEachLocationWithoutType()
    {
        // Arrange
        StoreCountsSample();

        // Act
        var result = await _sut.GetReadingCountsAsync(new GetReadingCountsRequest { GroupBy = [ReadingGrouping.Location] }, Ct);

        // Assert
        result.Value.ShouldBe(
        [
            new ReadingCountModel { Location = MeterLocation.Kitchen, Count = 5 },
            new ReadingCountModel { Location = MeterLocation.Bedroom, Count = 1 },
            new ReadingCountModel { Location = MeterLocation.Garage, Count = 1 }
        ]);
    }

    [Fact]
    public async Task GetReadingCountsAsync_NoGrouping_ReturnsTotal()
    {
        // Arrange
        StoreCountsSample();

        // Act
        var result = await _sut.GetReadingCountsAsync(new GetReadingCountsRequest(), Ct);

        // Assert
        result.Value.ShouldHaveSingleItem().ShouldBe(new ReadingCountModel { Count = 7 });
    }

    [Fact]
    public async Task GetReadingCountsAsync_NoGroupingAndNoReadings_ReturnsZeroTotal()
    {
        // Act
        var result = await _sut.GetReadingCountsAsync(new GetReadingCountsRequest(), Ct);

        // Assert
        result.Value.ShouldHaveSingleItem().ShouldBe(new ReadingCountModel { Count = 0 });
    }

    [Fact]
    public async Task GetReadingCountsAsync_DuplicateGrouping_GroupsOnce()
    {
        // Arrange
        StoreCountsSample();

        // Act
        var result = await _sut.GetReadingCountsAsync(
            new GetReadingCountsRequest { GroupBy = [ReadingGrouping.Type, ReadingGrouping.Type] },
            Ct);

        // Assert
        result.Value.Count.ShouldBe(3);
    }

    [Fact]
    public async Task GetReadingCountsAsync_Filter_CountsMatchingReadingsOnly()
    {
        // Arrange
        StoreCountsSample();

        var request = new GetReadingCountsRequest
        {
            Locations = [MeterLocation.Kitchen],
            From = Start.AddSeconds(1),
            GroupBy = [ReadingGrouping.Type]
        };

        // Act
        var result = await _sut.GetReadingCountsAsync(request, Ct);

        // Assert
        result.Value.ShouldBe(
        [
            new ReadingCountModel { Type = MeterType.Energy, Count = 1 },
            new ReadingCountModel { Type = MeterType.Motion, Count = 2 }
        ]);
    }

    [Fact]
    public async Task GetReadingCountsAsync_UndefinedGrouping_FailsWithoutQuerying()
    {
        // Act
        var result = await _sut.GetReadingCountsAsync(new GetReadingCountsRequest { GroupBy = [(ReadingGrouping)9] }, Ct);

        // Assert
        result.IsFailure.ShouldBeTrue();
        A.CallTo(() => _dbContext.Query<MeterReading>()).MustNotHaveHappened();
    }

    [Fact]
    public async Task GetReadingCountsAsync_InvalidFilter_FailsWithoutQuerying()
    {
        // Arrange
        var request = new GetReadingCountsRequest { Locations = [(MeterLocation)99] };

        // Act
        var result = await _sut.GetReadingCountsAsync(request, Ct);

        // Assert
        result.IsFailure.ShouldBeTrue();
        A.CallTo(() => _dbContext.Query<MeterReading>()).MustNotHaveHappened();
    }

    private static Guid IdEndingWith(int lastDigit)
    {
        return Guid.Parse($"00000000-0000-7000-8000-00000000000{lastDigit}");
    }

    // 7 readings: Kitchen 2 energy + 3 motion, Garage 1 energy, Bedroom 1 air quality.
    private void StoreCountsSample()
    {
        _storedReadings.AddRange(
        [
            StoredReadings.Energy(MeterLocation.Kitchen, Start),
            StoredReadings.Energy(MeterLocation.Kitchen, Start.AddSeconds(10)),
            StoredReadings.Motion(MeterLocation.Kitchen, Start),
            StoredReadings.Motion(MeterLocation.Kitchen, Start.AddSeconds(10)),
            StoredReadings.Motion(MeterLocation.Kitchen, Start.AddSeconds(20)),
            StoredReadings.Energy(MeterLocation.Garage, Start),
            StoredReadings.AirQuality(MeterLocation.Bedroom, Start)
        ]);
    }
}
