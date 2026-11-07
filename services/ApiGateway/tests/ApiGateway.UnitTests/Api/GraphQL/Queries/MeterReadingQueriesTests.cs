using System.Text.Json;
using ApiGateway.Api.GraphQL.Errors;
using ApiGateway.BusinessLogic.MeterReadings;
using ApiGateway.BusinessLogic.MeterReadings.Models;
using ApiGateway.BusinessLogic.MeterReadings.Requests;
using ApiGateway.BusinessLogic.Paging;
using ApiGateway.Common.Constants.Enums;
using ApiGateway.UnitTests.TestDoubles;
using ApiGateway.UnitTests.Utilities;
using Ausiannikau.Utilities.Results;
using FakeItEasy;
using HotChocolate;
using Shouldly;
using Xunit;

namespace ApiGateway.UnitTests.Api.GraphQL.Queries;

// Runs GraphQL requests through the real schema and pipeline (argument binding, types, paging, error filter)
// over a fake readings service.
public sealed class MeterReadingQueriesTests : IAsyncDisposable
{
    private static readonly DateTimeOffset ReceivedAt = new(2026, 11, 4, 12, 0, 0, TimeSpan.Zero);

    private static readonly EnergyReadingModel Energy = new()
    {
        Id = Guid.Parse("0199f5c4-3a1e-7b52-9d0e-5c8a7e6b4f21"),
        Location = MeterLocation.LivingRoom,
        ReceivedAt = ReceivedAt,
        Energy = 2.5
    };

    private static readonly MotionReadingModel Motion = new()
    {
        Id = Guid.Parse("0199f5c4-3a1e-7b52-9d0e-5c8a7e6b4f22"),
        Location = MeterLocation.Garage,
        ReceivedAt = ReceivedAt,
        MotionDetected = true
    };

    private static readonly AirQualityReadingModel AirQuality = new()
    {
        Id = Guid.Parse("0199f5c4-3a1e-7b52-9d0e-5c8a7e6b4f23"),
        Location = MeterLocation.Kitchen,
        ReceivedAt = ReceivedAt,
        Co2 = 812,
        Pm25 = 21,
        Humidity = 40
    };

    private readonly IMeterReadingsService _meterReadingsService = A.Fake<IMeterReadingsService>();
    private readonly GraphQLTestServer _server;

    public MeterReadingQueriesTests()
    {
        _server = new GraphQLTestServer(_meterReadingsService);
    }

    public async ValueTask DisposeAsync()
    {
        await _server.DisposeAsync();
    }

    [Fact]
    public async Task Readings_ServiceReturnsPage_ReturnsConnection()
    {
        // Arrange
        A.CallTo(() => _meterReadingsService.GetAllReadingsAsync(A<GetAllReadingsRequest>._, A<CancellationToken>._))
            .Returns(Result<CursorPage<MeterReadingModel>>.Succeed(new CursorPage<MeterReadingModel>
            {
                Edges =
                [
                    new CursorEdge<MeterReadingModel> { Node = Energy, Cursor = "cursor-1" },
                    new CursorEdge<MeterReadingModel> { Node = Motion, Cursor = "cursor-2" }
                ],
                HasNextPage = true,
                HasPreviousPage = false
            }));

        // Act
        var response = await _server.ExecuteAsync(
            """
            {
              readings {
                edges { cursor node { __typename id } }
                nodes { ... on EnergyReading { energy } ... on MotionReading { motionDetected } }
                pageInfo { hasNextPage hasPreviousPage startCursor endCursor }
              }
            }
            """);

        // Assert
        response.TryGetProperty("errors", out _).ShouldBeFalse();

        var readings = response.GetProperty("data").GetProperty("readings");

        var edges = readings.GetProperty("edges").EnumerateArray().ToList();
        edges.Select(e => e.GetProperty("cursor").GetString()).ShouldBe(["cursor-1", "cursor-2"]);
        edges.Select(e => e.GetProperty("node").GetProperty("__typename").GetString()).ShouldBe(["EnergyReading", "MotionReading"]);
        edges[0].GetProperty("node").GetProperty("id").GetGuid().ShouldBe(Energy.Id);

        var nodes = readings.GetProperty("nodes");
        nodes[0].GetProperty("energy").GetDouble().ShouldBe(2.5);
        nodes[1].GetProperty("motionDetected").GetBoolean().ShouldBeTrue();

        var pageInfo = readings.GetProperty("pageInfo");
        pageInfo.GetProperty("hasNextPage").GetBoolean().ShouldBeTrue();
        pageInfo.GetProperty("hasPreviousPage").GetBoolean().ShouldBeFalse();
        pageInfo.GetProperty("startCursor").GetString().ShouldBe("cursor-1");
        pageInfo.GetProperty("endCursor").GetString().ShouldBe("cursor-2");
    }

    [Fact]
    public async Task Readings_Arguments_ArePassedToService()
    {
        // Arrange
        ReturnEmptyPage();

        // Act
        await _server.ExecuteAsync(
            """
            {
              readings(
                filter: { types: [ENERGY], locations: [LIVING_ROOM, GARAGE], from: "2026-11-04T14:00:00+02:00", to: "2026-11-04T13:00:00Z" }
                first: 10
                after: "cursor-1"
              ) { pageInfo { hasNextPage } }
            }
            """);

        // Assert
        A.CallTo(() => _meterReadingsService.GetAllReadingsAsync(
                A<GetAllReadingsRequest>.That.Matches(r =>
                    r.Types!.SequenceEqual(new[] { MeterType.Energy })
                    && r.Locations!.SequenceEqual(new[] { MeterLocation.LivingRoom, MeterLocation.Garage })
                    && r.From == ReceivedAt
                    && r.To == ReceivedAt.AddHours(1)
                    && r.PageSize == 10
                    && r.Cursor == "cursor-1"),
                A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task Readings_NoArguments_PassesEmptyFilterAndDefaultPage()
    {
        // Arrange
        ReturnEmptyPage();

        // Act
        await _server.ExecuteAsync("{ readings { pageInfo { hasNextPage } } }");

        // Assert
        A.CallTo(() => _meterReadingsService.GetAllReadingsAsync(new GetAllReadingsRequest(), A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task Readings_FirstAboveMaxPageSize_ReturnsPagingErrorWithoutCallingService()
    {
        // Act
        var response = await _server.ExecuteAsync($"{{ readings(first: {PagingLimits.MaxPageSize + 1}) {{ pageInfo {{ hasNextPage }} }} }}");

        // Assert
        ErrorCode(response).ShouldBe(ErrorCodes.Paging.MaxPaginationItems);
        A.CallTo(_meterReadingsService).MustNotHaveHappened();
    }

    [Fact]
    public async Task Readings_FailedResult_ReturnsBadRequestWithResultMessage()
    {
        // Arrange
        A.CallTo(() => _meterReadingsService.GetAllReadingsAsync(A<GetAllReadingsRequest>._, A<CancellationToken>._))
            .Returns(Result<CursorPage<MeterReadingModel>>.Fail("'Cursor' is invalid."));

        // Act
        var response = await _server.ExecuteAsync("""{ readings(after: "garbage") { pageInfo { hasNextPage } } }""");

        // Assert
        var error = response.GetProperty("errors").EnumerateArray().ShouldHaveSingleItem();
        error.GetProperty("message").GetString().ShouldBe("'Cursor' is invalid.");
        error.GetProperty("extensions").GetProperty("code").GetString().ShouldBe(GraphQLErrorCodes.BadRequest);
        error.GetProperty("path")[0].GetString().ShouldBe("readings");
    }

    [Fact]
    public async Task LatestReadings_ServiceReturnsEachType_ResolvesConcreteTypes()
    {
        // Arrange
        A.CallTo(() => _meterReadingsService.GetLatestReadingsAsync(A<GetLatestReadingsRequest>._, A<CancellationToken>._))
            .Returns(Result<IReadOnlyList<MeterReadingModel>>.Succeed([AirQuality, Energy, Motion]));

        // Act
        var response = await _server.ExecuteAsync(
            """
            {
              latestReadings {
                __typename type location receivedAt
                ... on AirQualityReading { co2 pm25 humidity }
              }
            }
            """);

        // Assert
        var readings = response.GetProperty("data").GetProperty("latestReadings").EnumerateArray().ToList();
        readings.Select(r => r.GetProperty("__typename").GetString()).ShouldBe(["AirQualityReading", "EnergyReading", "MotionReading"]);
        readings.Select(r => r.GetProperty("type").GetString()).ShouldBe(["AIR_QUALITY", "ENERGY", "MOTION"]);
        readings.Select(r => r.GetProperty("location").GetString()).ShouldBe(["KITCHEN", "LIVING_ROOM", "GARAGE"]);
        readings[0].GetProperty("receivedAt").GetDateTimeOffset().ShouldBe(ReceivedAt);
        readings[0].GetProperty("co2").GetInt32().ShouldBe(812);
        readings[0].GetProperty("pm25").GetInt32().ShouldBe(21);
        readings[0].GetProperty("humidity").GetInt32().ShouldBe(40);
    }

    [Fact]
    public async Task LatestReadings_Filter_IsPassedToService()
    {
        // Arrange
        A.CallTo(() => _meterReadingsService.GetLatestReadingsAsync(A<GetLatestReadingsRequest>._, A<CancellationToken>._))
            .Returns(Result<IReadOnlyList<MeterReadingModel>>.Succeed([]));

        // Act
        await _server.ExecuteAsync("{ latestReadings(filter: { types: [MOTION], locations: [OFFICE] }) { id } }");

        // Assert
        A.CallTo(() => _meterReadingsService.GetLatestReadingsAsync(
                A<GetLatestReadingsRequest>.That.Matches(r =>
                    r.Types!.SequenceEqual(new[] { MeterType.Motion })
                    && r.Locations!.SequenceEqual(new[] { MeterLocation.Office })),
                A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task ReadingCounts_GroupBy_IsPassedToServiceAndUngroupedFieldsAreNull()
    {
        // Arrange
        A.CallTo(() => _meterReadingsService.GetReadingCountsAsync(A<GetReadingCountsRequest>._, A<CancellationToken>._))
            .Returns(Result<IReadOnlyList<ReadingCountModel>>.Succeed(
            [
                new ReadingCountModel { Location = MeterLocation.Bedroom, Count = 4 }
            ]));

        // Act
        var response = await _server.ExecuteAsync("{ readingCounts(groupBy: [LOCATION]) { type location count } }");

        // Assert
        var count = response.GetProperty("data").GetProperty("readingCounts").EnumerateArray().ShouldHaveSingleItem();
        count.GetProperty("type").ValueKind.ShouldBe(JsonValueKind.Null);
        count.GetProperty("location").GetString().ShouldBe("BEDROOM");
        count.GetProperty("count").GetInt32().ShouldBe(4);

        A.CallTo(() => _meterReadingsService.GetReadingCountsAsync(
                A<GetReadingCountsRequest>.That.Matches(r =>
                    r.Types == null
                    && r.Locations == null
                    && r.From == null
                    && r.To == null
                    && r.GroupBy.SequenceEqual(new[] { ReadingGrouping.Location })),
                A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task Query_DatabaseFailure_ReturnsServiceUnavailable()
    {
        // Arrange
        A.CallTo(() => _meterReadingsService.GetLatestReadingsAsync(A<GetLatestReadingsRequest>._, A<CancellationToken>._))
            .ThrowsAsync(new TestDbException("Failed to connect to 10.0.0.5:5432"));

        // Act
        var response = await _server.ExecuteAsync("{ latestReadings { id } }");

        // Assert
        ErrorCode(response).ShouldBe(GraphQLErrorCodes.ServiceUnavailable);
        ErrorMessage(response).ShouldNotContain("10.0.0.5");
    }

    [Fact]
    public async Task Query_UnexpectedFailure_ReturnsInternalErrorWithoutDetails()
    {
        // Arrange
        A.CallTo(() => _meterReadingsService.GetLatestReadingsAsync(A<GetLatestReadingsRequest>._, A<CancellationToken>._))
            .ThrowsAsync(new InvalidOperationException("Secret detail"));

        // Act
        var response = await _server.ExecuteAsync("{ latestReadings { id } }");

        // Assert
        ErrorCode(response).ShouldBe(GraphQLErrorCodes.InternalError);
        ErrorMessage(response).ShouldNotContain("Secret detail");
    }

    [Fact]
    public async Task Query_UnexpectedFailureInDevelopment_ShowsExceptionMessage()
    {
        // Arrange
        await using var server = new GraphQLTestServer(_meterReadingsService, environmentName: "Development");

        A.CallTo(() => _meterReadingsService.GetLatestReadingsAsync(A<GetLatestReadingsRequest>._, A<CancellationToken>._))
            .ThrowsAsync(new InvalidOperationException("Secret detail"));

        // Act
        var response = await server.ExecuteAsync("{ latestReadings { id } }");

        // Assert
        ErrorCode(response).ShouldBe(GraphQLErrorCodes.InternalError);
        ErrorMessage(response).ShouldBe("Secret detail");
    }

    private static string ErrorCode(JsonElement response)
    {
        return response.GetProperty("errors")[0].GetProperty("extensions").GetProperty("code").GetString()!;
    }

    private static string ErrorMessage(JsonElement response)
    {
        return response.GetProperty("errors")[0].GetProperty("message").GetString()!;
    }

    private void ReturnEmptyPage()
    {
        A.CallTo(() => _meterReadingsService.GetAllReadingsAsync(A<GetAllReadingsRequest>._, A<CancellationToken>._))
            .Returns(Result<CursorPage<MeterReadingModel>>.Succeed(new CursorPage<MeterReadingModel>
            {
                Edges = [],
                HasNextPage = false,
                HasPreviousPage = false
            }));
    }
}
