using FakeItEasy;
using Microsoft.AspNetCore.SignalR;
using NotificationService.Api.Hubs.Readings;
using NotificationService.Api.Hubs.Readings.Messages;
using NotificationService.Application.LiveReadings.Enums;
using NotificationService.Application.LiveReadings.Models;
using Xunit;

namespace NotificationService.UnitTests.Api.Hubs.Readings;

public sealed class SignalRReadingsNotifierTests
{
    private static readonly EnergyLiveReadingModel Reading = new()
    {
        Id = Guid.Parse("019a91b2-4c6e-7d13-9a58-3e7f2b6c1d02"),
        Location = MeterLocation.Office,
        ReceivedAt = new DateTimeOffset(2026, 11, 17, 10, 15, 30, TimeSpan.Zero),
        Energy = 845.62,
    };

    private readonly IHubContext<ReadingsHub, IReadingsClient> _hubContext = A.Fake<IHubContext<ReadingsHub, IReadingsClient>>();
    private readonly IReadingsClient _allClients = A.Fake<IReadingsClient>();
    private readonly SignalRReadingsNotifier _sut;

    public SignalRReadingsNotifierTests()
    {
        A.CallTo(() => _hubContext.Clients.All).Returns(_allClients);

        _sut = new SignalRReadingsNotifier(_hubContext);
    }

    [Fact]
    public async Task NotifyReadingStoredAsync_Reading_SendsItsMessageToAllClients()
    {
        // Act
        await _sut.NotifyReadingStoredAsync(Reading, TestContext.Current.CancellationToken);

        // Assert
        A.CallTo(() => _allClients.ReadingStored(
                new EnergyLiveReading
                {
                    Id = Reading.Id,
                    Location = Reading.Location,
                    ReceivedAt = Reading.ReceivedAt,
                    Energy = Reading.Energy,
                },
                A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task NotifyReadingStoredAsync_Token_PassesItToTheSend()
    {
        // Arrange
        using var cts = new CancellationTokenSource();

        // Act
        await _sut.NotifyReadingStoredAsync(Reading, cts.Token);

        // Assert
        A.CallTo(() => _allClients.ReadingStored(A<LiveReading>._, cts.Token)).MustHaveHappenedOnceExactly();
    }
}
