using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Xunit;

namespace ApiGateway.IntegrationTests.Utilities;

/// <summary>
/// Sends GraphQL requests to the gateway over HTTP, as the frontend's client does.
/// </summary>
internal sealed class GraphQLClient(HttpClient httpClient)
{
    private const string EndpointPath = "/graphql";

    public Task<GraphQLResponse> ExecuteAsync(string query, IReadOnlyDictionary<string, object?>? variables = null)
    {
        return SendAsync(JsonContent.Create(new { query, variables }));
    }

    /// <summary>
    /// Posts <paramref name="body"/> as is, for requests the client would never build (invalid JSON, no query).
    /// </summary>
    public Task<GraphQLResponse> PostRawAsync(string body)
    {
        return SendAsync(new StringContent(body, Encoding.UTF8, "application/json"));
    }

    private async Task<GraphQLResponse> SendAsync(HttpContent content)
    {
        var ct = TestContext.Current.CancellationToken;

        using var request = new HttpRequestMessage(HttpMethod.Post, EndpointPath)
        {
            Content = content
        };

        // The media types Apollo Client accepts; the response status codes follow the GraphQL over HTTP spec for them.
        request.Headers.Accept.ParseAdd("application/graphql-response+json");
        request.Headers.Accept.ParseAdd("application/json; q=0.9");

        using var response = await httpClient.SendAsync(request, ct);

        var responseContent = await response.Content.ReadAsStringAsync(ct);

        return new GraphQLResponse(
            response.StatusCode,
            responseContent,
            JsonDocument.Parse(responseContent).RootElement.Clone());
    }
}
