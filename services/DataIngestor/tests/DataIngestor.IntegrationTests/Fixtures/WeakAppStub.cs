using System.Net;
using WireMock;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace DataIngestor.IntegrationTests.Fixtures;

/// <summary>
/// An in-process stand-in for the WeakApp API, serving <c>GET /meters</c> with scripted responses.
/// </summary>
public sealed class WeakAppStub : IDisposable
{
    public const string ApiKey = "integration-tests-key";

    public const string MotionItem = """{"type":"motion","name":"Garage","payload":{"motionDetected":true}}""";
    public const string EnergyItem = """{"type":"energy","name":"Office","payload":{"energy":845.62}}""";
    public const string AirQualityItem = """{"type":"air_quality","name":"Kitchen","payload":{"co2":803,"pm25":25,"humidity":36}}""";

    public const string AllReadingsJson = $"[{MotionItem},{EnergyItem},{AirQualityItem}]";

    private const string MetersPath = "/meters";
    private const string ScenarioName = "meters";

    private readonly WireMockServer _server = WireMockServer.Start();

    public string BaseUrl => _server.Url!;

    public IReadOnlyList<IRequestMessage> MetersRequests =>
    [
        .. _server.LogEntries
            .Select(entry => entry.RequestMessage)
            .OfType<IRequestMessage>()
            .Where(request => request.Path == MetersPath)
    ];

    public static IResponseBuilder Readings(string json = AllReadingsJson)
    {
        return Response.Create()
            .WithStatusCode(HttpStatusCode.OK)
            .WithHeader("Content-Type", "application/json")
            .WithBody(json);
    }

    public static IResponseBuilder Status(HttpStatusCode statusCode)
    {
        return Response.Create()
            .WithStatusCode(statusCode)
            .WithHeader("Content-Type", "text/plain")
            .WithBody(statusCode.ToString());
    }

    public void RespondsWith(IResponseBuilder response)
    {
        RespondsInSequence(response);
    }

    /// <summary>
    /// Serves the responses one per request, in order; the last one is repeated for every further request.
    /// </summary>
    public void RespondsInSequence(params IResponseBuilder[] responses)
    {
        _server.Reset();

        for (var index = 0; index < responses.Length; index++)
        {
            var mapping = _server.Given(Request.Create().WithPath(MetersPath).UsingGet()).InScenario(ScenarioName);

            if (index > 0)
            {
                mapping = mapping.WhenStateIs(StateName(index));
            }

            if (index < responses.Length - 1)
            {
                mapping = mapping.WillSetStateTo(StateName(index + 1));
            }

            mapping.RespondWith(responses[index]);
        }
    }

    public void Dispose()
    {
        _server.Stop();
        _server.Dispose();
    }

    private static string StateName(int index)
    {
        return $"response-{index}";
    }
}
