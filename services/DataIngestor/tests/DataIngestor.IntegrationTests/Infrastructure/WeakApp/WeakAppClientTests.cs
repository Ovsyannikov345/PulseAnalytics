using System.Net;
using DataIngestor.Application.Interfaces.Clients;
using DataIngestor.Domain.Constants.Enums;
using DataIngestor.Domain.Entities;
using DataIngestor.IntegrationTests.Fixtures;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace DataIngestor.IntegrationTests.Infrastructure.WeakApp;

public sealed class WeakAppClientTests : IAsyncDisposable
{
    private const int MaxRetryAttempts = 2;

    private readonly WeakAppStub _weakApp = new();
    private readonly IngestorApplicationFactory _factory;
    private readonly IExternalMetersClient _sut;

    public WeakAppClientTests(RabbitMqContainerFixture rabbitMq)
    {
        _factory = new IngestorApplicationFactory(rabbitMq, _weakApp);
        _sut = _factory.Services.GetRequiredService<IExternalMetersClient>();
    }

    [Fact]
    public async Task GetMetersAsync_ApiReturnsReadings_RequestsMetersWithApiKey()
    {
        // Arrange
        _weakApp.RespondsWith(WeakAppStub.Readings());

        // Act
        await _sut.GetMetersAsync(TestContext.Current.CancellationToken);

        // Assert
        var request = _weakApp.MetersRequests.ShouldHaveSingleItem();
        request.Method.ShouldBe("GET");
        request.Headers.ShouldNotBeNull();
        request.Headers["X-Api-Key"].ShouldBe([WeakAppStub.ApiKey]);
    }

    [Fact]
    public async Task GetMetersAsync_ApiReturnsReadingsOfAllTypes_MapsEachReading()
    {
        // Arrange
        _weakApp.RespondsWith(WeakAppStub.Readings());

        // Act
        var result = await _sut.GetMetersAsync(TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Select(reading => (reading.Name, reading.Type, reading.Payload)).ShouldBe(
        [
            ("Garage", MeterType.Motion, new MotionPayload { MotionDetected = true }),
            ("Office", MeterType.Energy, new EnergyPayload { Energy = 845.62 }),
            ("Kitchen", MeterType.AirQuality, new AirQualityPayload { Co2 = 803, Pm25 = 25, Humidity = 36 })
        ]);
    }

    [Fact]
    public async Task GetMetersAsync_ResponseContainsMalformedItems_SkipsThemAndReturnsValidReadings()
    {
        // Arrange
        _weakApp.RespondsWith(WeakAppStub.Readings(
            $$$"""[{{{WeakAppStub.MotionItem}}},{"type":"thermostat","name":"Hall","payload":{}},{"type":"energy","name":"Office"},null]"""));

        // Act
        var result = await _sut.GetMetersAsync(TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldHaveSingleItem().Name.ShouldBe("Garage");
    }

    [Fact]
    public async Task GetMetersAsync_ApiFailsTransientlyThenRecovers_RetriesAndReturnsReadings()
    {
        // Arrange
        _weakApp.RespondsInSequence(WeakAppStub.Status(HttpStatusCode.InternalServerError), WeakAppStub.Readings());

        // Act
        var result = await _sut.GetMetersAsync(TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Count.ShouldBe(3);
        _weakApp.MetersRequests.Count.ShouldBe(2);
    }

    [Fact]
    public async Task GetMetersAsync_ApiRespondsSlowerThanAttemptTimeout_RetriesAndReturnsReadings()
    {
        // Arrange
        // The slow response differs from the fast one, so the result shows which of them the client used.
        var slowResponse = WeakAppStub.Readings($"[{WeakAppStub.MotionItem}]").WithDelay(TimeSpan.FromSeconds(3));
        _weakApp.RespondsInSequence(slowResponse, WeakAppStub.Readings());

        // Act
        var result = await _sut.GetMetersAsync(TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Count.ShouldBe(3);
    }

    [Fact]
    public async Task GetMetersAsync_ApiRateLimits_ReturnsFailureWithoutRetrying()
    {
        // Arrange
        _weakApp.RespondsWith(WeakAppStub.Status(HttpStatusCode.TooManyRequests));

        // Act
        var result = await _sut.GetMetersAsync(TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.ErrorMessage.ShouldBe("Weak App API responded with failure");
        _weakApp.MetersRequests.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task GetMetersAsync_ApiKeepsFailing_ReturnsFailureAfterAllRetries()
    {
        // Arrange
        _weakApp.RespondsWith(WeakAppStub.Status(HttpStatusCode.ServiceUnavailable));

        // Act
        var result = await _sut.GetMetersAsync(TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.ErrorMessage.ShouldBe("Weak App API responded with failure");
        _weakApp.MetersRequests.Count.ShouldBe(1 + MaxRetryAttempts);
    }

    public async ValueTask DisposeAsync()
    {
        await _factory.DisposeAsync();
        _weakApp.Dispose();
    }
}
