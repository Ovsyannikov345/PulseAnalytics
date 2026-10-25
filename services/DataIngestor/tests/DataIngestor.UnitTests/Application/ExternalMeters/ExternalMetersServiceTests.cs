using Ausiannikau.Utilities.Results;
using DataIngestor.Application.ExternalMeters;
using DataIngestor.Application.Interfaces.Clients;
using DataIngestor.Application.Interfaces.Messaging;
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
    private readonly IMeterReadingsPublisher _readingsPublisher = A.Fake<IMeterReadingsPublisher>();
    private readonly FakeLogger<ExternalMetersService> _logger = new();
    private readonly ExternalMetersService _sut;

    public ExternalMetersServiceTests()
    {
        A.CallTo(() => _readingsPublisher.PublishAsync(A<IReadOnlyCollection<MeterReading>>._, A<CancellationToken>._))
            .Returns(Result.Succeed());

        _sut = new ExternalMetersService(_metersClient, _readingsPublisher, _logger);
    }

    [Fact]
    public async Task IngestReadingsAsync_ClientReturnsReadings_ReturnsSuccess()
    {
        // Arrange
        ClientReturns(CreateMotionReading("Garage"), CreateEnergyReading("Office"));

        // Act
        var result = await _sut.IngestReadingsAsync(TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        _logger.Collector.GetSnapshot().ShouldNotContain(entry => entry.Level >= LogLevel.Warning);
    }

    [Fact]
    public async Task IngestReadingsAsync_ClientReturnsReadings_HandsOffEveryReading()
    {
        // Arrange
        ClientReturns(CreateMotionReading("Garage"), CreateEnergyReading("Office"), CreateMotionReading("Bedroom"));

        // Act
        await _sut.IngestReadingsAsync(TestContext.Current.CancellationToken);

        // Assert
        A.CallTo(() => _readingsPublisher.PublishAsync(
                A<IReadOnlyCollection<MeterReading>>.That.Matches(readings => readings.Count == 3),
                A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task IngestReadingsAsync_ClientReturnsNoReadings_ReturnsSuccessWithoutHandingOffAnything()
    {
        // Arrange
        ClientReturns();

        // Act
        var result = await _sut.IngestReadingsAsync(TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        A.CallTo(_readingsPublisher).MustNotHaveHappened();
    }

    [Fact]
    public async Task IngestReadingsAsync_ClientReturnsNoReadings_LogsThatThereIsNothingToPublish()
    {
        // Arrange
        ClientReturns();

        // Act
        await _sut.IngestReadingsAsync(TestContext.Current.CancellationToken);

        // Assert
        var entry = _logger.Collector.GetSnapshot().ShouldHaveSingleItem();
        entry.Level.ShouldBe(LogLevel.Information);
        entry.Message.ShouldBe("No readings were received, nothing to publish");
    }

    [Fact]
    public async Task IngestReadingsAsync_ClientReturnsReadings_PassesExactReadingsAndCancellationTokenToPublisher()
    {
        // Arrange
        MeterReading[] readings = [CreateMotionReading("Garage"), CreateEnergyReading("Office")];
        ClientReturns(readings);
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);

        // Act
        await _sut.IngestReadingsAsync(cts.Token);

        // Assert
        A.CallTo(() => _readingsPublisher.PublishAsync(
                A<IReadOnlyCollection<MeterReading>>.That.IsSameSequenceAs(readings),
                cts.Token))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task IngestReadingsAsync_PublisherFails_ReturnsFailureWithPublisherErrorMessage()
    {
        // Arrange
        ClientReturns(CreateMotionReading("Garage"));
        PublisherFails("Failed to publish 1 of 1 meter readings");

        // Act
        var result = await _sut.IngestReadingsAsync(TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.ErrorMessage.ShouldBe("Failed to publish 1 of 1 meter readings");
    }

    [Fact]
    public async Task IngestReadingsAsync_PublisherFails_LogsErrorWithReason()
    {
        // Arrange
        ClientReturns(CreateMotionReading("Garage"));
        PublisherFails("Failed to publish 1 of 1 meter readings");

        // Act
        await _sut.IngestReadingsAsync(TestContext.Current.CancellationToken);

        // Assert
        var error = _logger.Collector.GetSnapshot().Where(entry => entry.Level == LogLevel.Error).ShouldHaveSingleItem();
        error.Message.ShouldBe("Failed to publish readings. Reason: Failed to publish 1 of 1 meter readings");
    }

    [Fact]
    public async Task IngestReadingsAsync_ClientFails_ReturnsFailureWithClientErrorMessage()
    {
        // Arrange
        ClientFails("Weak App API responded with failure");

        // Act
        var result = await _sut.IngestReadingsAsync(TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.ErrorMessage.ShouldBe("Weak App API responded with failure");
    }

    [Fact]
    public async Task IngestReadingsAsync_ClientFails_LogsErrorWithReason()
    {
        // Arrange
        ClientFails("Weak App API responded with failure");

        // Act
        await _sut.IngestReadingsAsync(TestContext.Current.CancellationToken);

        // Assert
        var error = _logger.Collector.GetSnapshot().Where(entry => entry.Level == LogLevel.Error).ShouldHaveSingleItem();
        error.Message.ShouldBe("Failed to extract readings. Reason: Weak App API responded with failure");
        A.CallTo(_readingsPublisher).MustNotHaveHappened();
    }

    [Fact]
    public async Task IngestReadingsAsync_AnyCall_PassesCancellationTokenToClient()
    {
        // Arrange
        ClientReturns();
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);

        // Act
        await _sut.IngestReadingsAsync(cts.Token);

        // Assert
        A.CallTo(() => _metersClient.GetMetersAsync(cts.Token)).MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task IngestReadingsAsync_ClientThrows_PropagatesException()
    {
        // Arrange
        var exception = new InvalidOperationException("Unexpected client failure");
        A.CallTo(() => _metersClient.GetMetersAsync(A<CancellationToken>._)).ThrowsAsync(exception);

        // Act
        var act = () => _sut.IngestReadingsAsync(TestContext.Current.CancellationToken);

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

    private void PublisherFails(string errorMessage)
    {
        A.CallTo(() => _readingsPublisher.PublishAsync(A<IReadOnlyCollection<MeterReading>>._, A<CancellationToken>._))
            .Returns(Result.Fail(errorMessage));
    }
}
