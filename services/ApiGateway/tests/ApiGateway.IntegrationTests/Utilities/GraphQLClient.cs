using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace ApiGateway.IntegrationTests.Utilities;

/// <summary>
/// Sends GraphQL requests to the gateway over HTTP, as the frontend's client does.
/// </summary>
internal sealed class GraphQLClient(HttpClient httpClient)
{
    private const string EndpointPath = "/graphql";

    public async Task<GraphQLResponse> ExecuteAsync(string query, IReadOnlyDictionary<string, object?>? variables = null)
    {
        var ct = TestContext.Current.CancellationToken;

        using var request = new HttpRequestMessage(HttpMethod.Post, EndpointPath)
        {
            Content = JsonContent.Create(new { query, variables })
        };

        // The media types Apollo Client accepts; the response status codes follow the GraphQL over HTTP spec for them.
        request.Headers.Accept.ParseAdd("application/graphql-response+json");
        request.Headers.Accept.ParseAdd("application/json; q=0.9");

        using var response = await httpClient.SendAsync(request, ct);

        var content = await response.Content.ReadAsStringAsync(ct);

        return new GraphQLResponse(response.StatusCode, content, JsonDocument.Parse(content).RootElement.Clone());
    }
}
