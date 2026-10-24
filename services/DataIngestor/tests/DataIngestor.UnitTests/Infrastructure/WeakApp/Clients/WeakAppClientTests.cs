using System.Net;
using System.Text.Json;
using DataIngestor.Domain.Constants.Enums;
using DataIngestor.Domain.Entities;
using DataIngestor.Infrastructure.WeakApp.Clients;
using DataIngestor.UnitTests.TestDoubles;
using FakeItEasy;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using Microsoft.Extensions.Time.Testing;
using Shouldly;
using Xunit;

namespace DataIngestor.UnitTests.Infrastructure.WeakApp.Clients;

public sealed class WeakAppClientTests : IDisposable
{
    private const string MotionItem = """{"type":"motion","name":"Garage","payload":{"motionDetected":true}}""";
    private const string EnergyItem = """{"type":"energy","name":"Office","payload":{"energy":845.62}}""";
    private const string AirQualityItem = """{"type":"air_quality","name":"Kitchen","payload":{"co2":803,"pm25":25,"humidity":36}}""";

    private const string SendFailureMessage = "Error occurred while sending HTTP request";
    private const string FailureStatusMessage = "Weak App API responded with failure";
    private const string UnreadableBodyMessage = "Failed to read Weak App API response";

    private static readonly DateTimeOffset Now = new(2026, 10, 24, 12, 0, 0, TimeSpan.Zero);

    private readonly HttpMessageHandler _handler = A.Fake<HttpMessageHandler>();
    private readonly FakeTimeProvider _timeProvider = new(Now);
    private readonly FakeLogger<WeakAppClient> _logger = new();
    private readonly HttpClient _httpClient;
    private readonly WeakAppClient _sut;

    public WeakAppClientTests()
    {
        _httpClient = new HttpClient(_handler) { BaseAddress = new Uri("http://weakapp.test/") };

        _sut = new WeakAppClient(_httpClient, _timeProvider, _logger);
    }

    public void Dispose()
    {
        _httpClient.Dispose();
    }

    [Fact]
    public async Task GetMetersAsync_AnyCall_SendsGetRequestToMetersEndpointOfBaseAddress()
    {
        // Arrange
        _handler.RespondsWithJson("[]");

        // Act
        await _sut.GetMetersAsync(TestContext.Current.CancellationToken);

        // Assert
        var request = _handler.SingleReceivedRequest();
        request.Method.ShouldBe(HttpMethod.Get);
        request.RequestUri.ShouldBe(new Uri("http://weakapp.test/meters"));
    }

    [Fact]
    public async Task GetMetersAsync_MotionReading_MapsToMotionMeterReading()
    {
        // Arrange
        _handler.RespondsWithJson($"[{MotionItem}]");

        // Act
        var result = await _sut.GetMetersAsync(TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldHaveSingleItem().ShouldBe(new MeterReading
        {
            Name = "Garage",
            Type = MeterType.Motion,
            ReceivedAt = Now,
            Payload = new MotionPayload { MotionDetected = true }
        });
    }

    [Fact]
    public async Task GetMetersAsync_EnergyReading_MapsToEnergyMeterReading()
    {
        // Arrange
        _handler.RespondsWithJson($"[{EnergyItem}]");

        // Act
        var result = await _sut.GetMetersAsync(TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldHaveSingleItem().ShouldBe(new MeterReading
        {
            Name = "Office",
            Type = MeterType.Energy,
            ReceivedAt = Now,
            Payload = new EnergyPayload { Energy = 845.62 }
        });
    }

    [Fact]
    public async Task GetMetersAsync_AirQualityReading_MapsToAirQualityMeterReading()
    {
        // Arrange
        _handler.RespondsWithJson($"[{AirQualityItem}]");

        // Act
        var result = await _sut.GetMetersAsync(TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldHaveSingleItem().ShouldBe(new MeterReading
        {
            Name = "Kitchen",
            Type = MeterType.AirQuality,
            ReceivedAt = Now,
            Payload = new AirQualityPayload { Co2 = 803, Pm25 = 25, Humidity = 36 }
        });
    }

    [Fact]
    public async Task GetMetersAsync_ReadingsOfAllTypes_ReturnsAllInResponseOrder()
    {
        // Arrange
        _handler.RespondsWithJson($"[{EnergyItem},{MotionItem},{AirQualityItem}]");

        // Act
        var result = await _sut.GetMetersAsync(TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Select(reading => (reading.Name, reading.Type)).ShouldBe(
        [
            ("Office", MeterType.Energy),
            ("Garage", MeterType.Motion),
            ("Kitchen", MeterType.AirQuality)
        ]);
    }

    [Fact]
    public async Task GetMetersAsync_SuccessfulResponse_StampsAllReadingsWithTimeResponseWasReceived()
    {
        // Arrange
        var responseDelay = TimeSpan.FromSeconds(2);
        _handler.CallsToSendAsync()
            .Invokes(() => _timeProvider.Advance(responseDelay))
            .ReturnsLazily(() => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent($"[{MotionItem},{EnergyItem},{AirQualityItem}]")
            }));

        // Act
        var result = await _sut.GetMetersAsync(TestContext.Current.CancellationToken);

        // Assert
        result.Value.Count.ShouldBe(3);
        result.Value.ShouldAllBe(reading => reading.ReceivedAt == Now + responseDelay);
    }

    [Theory]
    [InlineData(HttpStatusCode.OK, "[]")]
    [InlineData(HttpStatusCode.OK, """{"error":"data corrupted"}""")]
    [InlineData(HttpStatusCode.BadGateway, "Bad Gateway")]
    public async Task GetMetersAsync_AnyResponse_DisposesIt(HttpStatusCode statusCode, string body)
    {
        // Arrange
        var content = new DisposalTrackingContent(body);
        _handler.RespondsWith(statusCode, () => content);

        // Act
        await _sut.GetMetersAsync(TestContext.Current.CancellationToken);

        // Assert
        content.IsDisposed.ShouldBeTrue();
    }

    [Fact]
    public async Task GetMetersAsync_SuccessfulResponse_LogsNoWarningsOrErrors()
    {
        // Arrange
        _handler.RespondsWithJson($"[{MotionItem},{EnergyItem},{AirQualityItem}]");

        // Act
        await _sut.GetMetersAsync(TestContext.Current.CancellationToken);

        // Assert
        _logger.Collector.GetSnapshot().ShouldNotContain(entry => entry.Level >= LogLevel.Warning);
    }

    [Fact]
    public async Task GetMetersAsync_WholeNumberEnergy_MapsToDouble()
    {
        // Arrange
        _handler.RespondsWithJson("""[{"type":"energy","name":"Office","payload":{"energy":946}}]""");

        // Act
        var result = await _sut.GetMetersAsync(TestContext.Current.CancellationToken);

        // Assert
        var reading = result.Value.ShouldHaveSingleItem();
        reading.Payload.ShouldBeOfType<EnergyPayload>().Energy.ShouldBe(946d);
    }

    [Fact]
    public async Task GetMetersAsync_TypeDiscriminatorIsNotTheFirstProperty_ParsesReading()
    {
        // Arrange
        _handler.RespondsWithJson("""[{"name":"Garage","payload":{"motionDetected":false},"type":"motion"}]""");

        // Act
        var result = await _sut.GetMetersAsync(TestContext.Current.CancellationToken);

        // Assert
        var reading = result.Value.ShouldHaveSingleItem();
        reading.Type.ShouldBe(MeterType.Motion);
        reading.Payload.ShouldBe(new MotionPayload { MotionDetected = false });
    }

    [Fact]
    public async Task GetMetersAsync_UnknownPropertiesPresent_IgnoresThem()
    {
        // Arrange
        _handler.RespondsWithJson("""[{"type":"motion","id":7,"name":"Garage","payload":{"motionDetected":true,"battery":90}}]""");

        // Act
        var result = await _sut.GetMetersAsync(TestContext.Current.CancellationToken);

        // Assert
        var reading = result.Value.ShouldHaveSingleItem();
        reading.Name.ShouldBe("Garage");
        reading.Payload.ShouldBe(new MotionPayload { MotionDetected = true });
    }

    [Fact]
    public async Task GetMetersAsync_EmptyArray_ReturnsSuccessWithNoReadings()
    {
        // Arrange
        _handler.RespondsWithJson("[]");

        // Act
        var result = await _sut.GetMetersAsync(TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("""{"type":"temperature","name":"Garage","payload":{"celsius":21}}""")]
    [InlineData("""{"type":"Motion","name":"Garage","payload":{"motionDetected":true}}""")]
    [InlineData("""{"name":"Garage","payload":{"motionDetected":true}}""")]
    [InlineData("""{"type":null,"name":"Garage","payload":{"motionDetected":true}}""")]
    [InlineData("""{"type":"motion","payload":{"motionDetected":true}}""")]
    [InlineData("""{"type":"motion","name":null,"payload":{"motionDetected":true}}""")]
    [InlineData("""{"type":"motion","name":"Garage"}""")]
    [InlineData("""{"type":"motion","name":"Garage","payload":null}""")]
    [InlineData("""{"type":"motion","name":"Garage","payload":{"motionDetected":"yes"}}""")]
    [InlineData("""{"type":"energy","name":"Office","payload":{"energy":"845.62"}}""")]
    [InlineData("""{"type":"air_quality","name":"Kitchen","payload":{"co2":803,"pm25":25}}""")]
    [InlineData("""{"type":"air_quality","name":"Kitchen","payload":{"co2":803.5,"pm25":25,"humidity":36}}""")]
    [InlineData("42")]
    [InlineData("\"motion\"")]
    [InlineData("[]")]
    public async Task GetMetersAsync_MalformedItem_SkipsItAndKeepsValidReadings(string malformedItem)
    {
        // Arrange
        _handler.RespondsWithJson($"[{MotionItem},{malformedItem},{EnergyItem}]");

        // Act
        var result = await _sut.GetMetersAsync(TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Select(reading => reading.Type).ShouldBe([MeterType.Motion, MeterType.Energy]);
        var warning = _logger.Collector.GetSnapshot().Where(entry => entry.Level == LogLevel.Warning).ShouldHaveSingleItem();
        warning.Message.ShouldBe("Skipped malformed meter reading at index 1");
        warning.Exception.ShouldNotBeNull();
    }

    [Fact]
    public async Task GetMetersAsync_NullItem_SkipsItAndKeepsValidReadings()
    {
        // Arrange
        _handler.RespondsWithJson($"[{MotionItem},null,{EnergyItem}]");

        // Act
        var result = await _sut.GetMetersAsync(TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Select(reading => reading.Type).ShouldBe([MeterType.Motion, MeterType.Energy]);
        var warning = _logger.Collector.GetSnapshot().Where(entry => entry.Level == LogLevel.Warning).ShouldHaveSingleItem();
        warning.Message.ShouldBe("Skipped null meter reading at index 1");
    }

    [Fact]
    public async Task GetMetersAsync_AllItemsMalformed_ReturnsSuccessWithNoReadings()
    {
        // Arrange
        _handler.RespondsWithJson("""[{"type":"unknown","name":"Garage"},{"name":"Office"},null]""");

        // Act
        var result = await _sut.GetMetersAsync(TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBeEmpty();
        _logger.Collector.GetSnapshot().Count(entry => entry.Level == LogLevel.Warning).ShouldBe(3);
    }

    [Theory]
    [InlineData("""{"error":"data corrupted"}""")]
    [InlineData("{}")]
    [InlineData("\"OK\"")]
    [InlineData("42")]
    [InlineData("")]
    [InlineData("not json")]
    [InlineData("""[{"type":"motion","name":"Garage","payload":{"motionDetected":true}}""")]
    public async Task GetMetersAsync_BodyIsNotAValidJsonArray_ReturnsFailure(string body)
    {
        // Arrange
        _handler.RespondsWithJson(body);

        // Act
        var result = await _sut.GetMetersAsync(TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.ErrorMessage.ShouldBe(UnreadableBodyMessage);
    }

    [Fact]
    public async Task GetMetersAsync_BodyIsNotAJsonArray_LogsDeserializationError()
    {
        // Arrange
        _handler.RespondsWithJson("""{"error":"data corrupted"}""");

        // Act
        await _sut.GetMetersAsync(TestContext.Current.CancellationToken);

        // Assert
        var error = _logger.Collector.GetSnapshot().Where(entry => entry.Level == LogLevel.Error).ShouldHaveSingleItem();
        error.Message.ShouldBe("Exception occurred while deserializing response body");
        error.Exception.ShouldBeOfType<JsonException>();
    }

    [Fact]
    public async Task GetMetersAsync_BodyIsJsonNull_ReturnsFailureAndLogsEmptyBody()
    {
        // Arrange
        _handler.RespondsWithJson("null");

        // Act
        var result = await _sut.GetMetersAsync(TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.ErrorMessage.ShouldBe(UnreadableBodyMessage);
        var error = _logger.Collector.GetSnapshot().Where(entry => entry.Level == LogLevel.Error).ShouldHaveSingleItem();
        error.Message.ShouldBe("Response body is empty");
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, "Invalid or missing API key")]
    [InlineData(HttpStatusCode.NotFound, "")]
    [InlineData(HttpStatusCode.TooManyRequests, "Too Many Requests: rate limit exceeded")]
    [InlineData(HttpStatusCode.InternalServerError, "Unknown error")]
    [InlineData(HttpStatusCode.BadGateway, "Bad Gateway")]
    [InlineData(HttpStatusCode.GatewayTimeout, "Gateway Timeout")]
    public async Task GetMetersAsync_NonSuccessStatusCode_ReturnsFailure(HttpStatusCode statusCode, string body)
    {
        // Arrange
        _handler.RespondsWith(statusCode, body);

        // Act
        var result = await _sut.GetMetersAsync(TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.ErrorMessage.ShouldBe(FailureStatusMessage);
    }

    [Fact]
    public async Task GetMetersAsync_NonSuccessStatusCode_LogsWarningWithRequestAndResponseDetails()
    {
        // Arrange
        _handler.RespondsWith(HttpStatusCode.BadGateway, "Bad Gateway");

        // Act
        await _sut.GetMetersAsync(TestContext.Current.CancellationToken);

        // Assert
        var warning = _logger.Collector.GetSnapshot().Where(entry => entry.Level == LogLevel.Warning).ShouldHaveSingleItem();
        warning.Message.ShouldBe(
            "API responded with failure. HTTP method: GET. URL: http://weakapp.test/meters. " +
            "Status code: 502. Response body: Bad Gateway");
    }

    [Fact]
    public async Task GetMetersAsync_NonSuccessStatusCodeWithReadingsInBody_ReturnsFailure()
    {
        // Arrange
        _handler.RespondsWith(HttpStatusCode.InternalServerError, $"[{MotionItem}]");

        // Act
        var result = await _sut.GetMetersAsync(TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.ErrorMessage.ShouldBe(FailureStatusMessage);
    }

    [Fact]
    public async Task GetMetersAsync_RequestThrows_ReturnsFailureAndLogsException()
    {
        // Arrange
        var exception = new HttpRequestException("Connection refused");
        _handler.CallsToSendAsync().ThrowsAsync(exception);

        // Act
        var result = await _sut.GetMetersAsync(TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.ErrorMessage.ShouldBe(SendFailureMessage);
        var error = _logger.Collector.GetSnapshot().Where(entry => entry.Level == LogLevel.Error).ShouldHaveSingleItem();
        error.Message.ShouldBe("Exception occurred while sending HTTP request");
        error.Exception.ShouldBeSameAs(exception);
    }

    [Fact]
    public async Task GetMetersAsync_RequestTimesOut_ReturnsFailure()
    {
        // Arrange
        // Simulates a cancellation the caller did not request, e.g. an HttpClient or resilience-pipeline timeout.
        _handler.CallsToSendAsync().ThrowsAsync(new TaskCanceledException("The request timed out", new TimeoutException()));

        // Act
        var result = await _sut.GetMetersAsync(TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.ErrorMessage.ShouldBe(SendFailureMessage);
    }

    [Fact]
    public async Task GetMetersAsync_ResponseBodyCannotBeRead_ReturnsSendFailure()
    {
        // Arrange
        // HttpClient buffers the body before returning the response, so a body that fails
        // mid-read surfaces as a failed request rather than a deserialization failure.
        _handler.RespondsWith(HttpStatusCode.OK, () => new UnreadableHttpContent());

        // Act
        var result = await _sut.GetMetersAsync(TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.ErrorMessage.ShouldBe(SendFailureMessage);
    }

    [Fact]
    public async Task GetMetersAsync_CallerCancelsWhileRequestIsInFlight_ThrowsOperationCanceledException()
    {
        // Arrange
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        _handler.CancelsCallerWhileInFlight(cts);

        // Act
        var act = () => _sut.GetMetersAsync(cts.Token);

        // Assert
        await act.ShouldThrowAsync<OperationCanceledException>();
        _logger.Collector.GetSnapshot().ShouldNotContain(entry => entry.Level >= LogLevel.Warning);
    }
}
