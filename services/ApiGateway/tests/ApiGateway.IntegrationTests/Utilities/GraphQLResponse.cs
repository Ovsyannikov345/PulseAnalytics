using System.Net;
using System.Text.Json;

namespace ApiGateway.IntegrationTests.Utilities;

/// <summary>
/// A GraphQL response as received over HTTP: the status code, the raw body and the parsed body.
/// </summary>
internal sealed record GraphQLResponse(HttpStatusCode StatusCode, string Content, JsonElement Body)
{
    public JsonElement Data => Body.GetProperty("data");

    public IReadOnlyList<JsonElement> Errors =>
        Body.TryGetProperty("errors", out var errors) ? [.. errors.EnumerateArray()] : [];
}
