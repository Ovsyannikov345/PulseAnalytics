using Ausiannikau.Utilities.Results;
using DataIngestor.Application.ExternalMeters;
using DataIngestor.Application.Interfaces.Clients;
using DataIngestor.Domain.Constants.Enums;
using DataIngestor.Domain.Entities;
using FakeItEasy;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using Shouldly;
using Xunit;

namespace DataIngestor.UnitTests.Application.ExternalMeters;

public sealed class ExternalMetersServiceTests
{
    private static readonly DateTimeOffset ReadingTime = new(2026, 10, 24, 12, 0, 0, TimeSpan.Zero);

    private readonly IExternalMetersClient _metersClient = A.Fake<IExternalMetersClient>();
    private readonly FakeLogger<ExternalMetersService> _logger = new();
    private readonly ExternalMetersService _sut;

    public ExternalMetersServiceTests()
    {
        _sut = new ExternalMetersService(_metersClient, _logger);
    }

    [Fact]
    public async Task ExtractReadingsAsync_ClientReturnsReadings_ReturnsSuccess()
    {
        // Arrange
        ClientReturns(CreateMotionReading("Garage"), CreateEnergyReading("Office"));

        // Act
        var result = await _sut.ExtractReadingsAsync(TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        _logger.Collector.GetSnapshot().ShouldNotContain(entry => entry.Level >= LogLevel.Warning);
    }

    [Fact]
    public async Task ExtractReadingsAsync_ClientReturnsReadings_HandsOffEveryReading()
    {
        // Arrange
        ClientReturns(CreateMotionReading("Garage"), CreateEnergyReading("Office"), CreateMotionReading("Bedroom"));

        // Act
        await _sut.ExtractReadingsAsync(TestContext.Current.CancellationToken);

        // Assert
        // Publishing is still a logging placeholder (TODO in ExternalMetersService);
        // replace this with an assertion on the publisher once it exists.
        _logger.Collector.GetSnapshot()
            .Count(entry => entry.Message.StartsWith("Publishing reading:", StringComparison.Ordinal))
            .ShouldBe(3);
    }

    [Fact]
    public async Task ExtractReadingsAsync_ClientReturnsNoReadings_ReturnsSuccessWithoutHandingOffAnything()
    {
        // Arrange
        ClientReturns();

        // Act
        var result = await _sut.ExtractReadingsAsync(TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        _logger.Collector.GetSnapshot().ShouldNotContain(entry => entry.Message.StartsWith("Publishing reading:", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ExtractReadingsAsync_ClientFails_ReturnsFailureWithClientErrorMessage()
    {
        // Arrange
        ClientFails("Weak App API responded with failure");

        // Act
        var result = await _sut.ExtractReadingsAsync(TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.ErrorMessage.ShouldBe("Weak App API responded with failure");
    }

    [Fact]
    public async Task ExtractReadingsAsync_ClientFails_LogsErrorWithReason()
    {
        // Arrange
        ClientFails("Weak App API responded with failure");

        // Act
        await _sut.ExtractReadingsAsync(TestContext.Current.CancellationToken);

        // Assert
        var error = _logger.Collector.GetSnapshot().Where(entry => entry.Level == LogLevel.Error).ShouldHaveSingleItem();
        error.Message.ShouldBe("Failed to extract readings. Reason: Weak App API responded with failure");
        _logger.Collector.GetSnapshot().ShouldNotContain(entry => entry.Message.StartsWith("Publishing reading:", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ExtractReadingsAsync_AnyCall_PassesCancellationTokenToClient()
    {
        // Arrange
        ClientReturns();
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);

        // Act
        await _sut.ExtractReadingsAsync(cts.Token);

        // Assert
        A.CallTo(() => _metersClient.GetMetersAsync(cts.Token)).MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task ExtractReadingsAsync_ClientThrows_PropagatesException()
    {
        // Arrange
        var exception = new InvalidOperationException("Unexpected client failure");
        A.CallTo(() => _metersClient.GetMetersAsync(A<CancellationToken>._)).ThrowsAsync(exception);

        // Act
        var act = () => _sut.ExtractReadingsAsync(TestContext.Current.CancellationToken);

        // Assert
        var thrown = await act.ShouldThrowAsync<InvalidOperationException>();
        thrown.ShouldBeSameAs(exception);
    }

    private static MeterReading CreateMotionReading(string name)
    {
        return new MeterReading
        {
            Name = name,
            Type = MeterType.Motion,
            ReceivedAt = ReadingTime,
            Payload = new MotionPayload { MotionDetected = true }
        };
    }

    private static MeterReading CreateEnergyReading(string name)
    {
        return new MeterReading
        {
            Name = name,
            Type = MeterType.Energy,
            ReceivedAt = ReadingTime,
            Payload = new EnergyPayload { Energy = 845.62 }
        };
    }

    private void ClientReturns(params MeterReading[] readings)
    {
        A.CallTo(() => _metersClient.GetMetersAsync(A<CancellationToken>._))
            .Returns(Result<List<MeterReading>>.Succeed([.. readings]));
    }

    private void ClientFails(string errorMessage)
    {
        A.CallTo(() => _metersClient.GetMetersAsync(A<CancellationToken>._))
            .Returns(Result<List<MeterReading>>.Fail(errorMessage));
    }
}
