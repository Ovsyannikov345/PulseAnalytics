using Ausiannikau.Utilities.Results;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace DataIngestor.Infrastructure.WeakApp.Clients;

internal abstract class BaseClient(HttpClient httpClient, ILogger logger)
{
    protected abstract JsonSerializerOptions SerializerOptions { get; }

    protected readonly ILogger Logger = logger;

    protected async Task<Result<HttpResponseMessage>> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        try
        {
            var response = await httpClient.SendAsync(request, ct);

            return response;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            Logger.FailedToSend(ex);

            return Result<HttpResponseMessage>.Fail("Error occurred while sending HTTP request");
        }
    }

    protected async Task<Result<TPayload>> DeserializeAsync<TPayload>(HttpResponseMessage response, CancellationToken ct)
    {
        try
        {
            var bodyStream = await response.Content.ReadAsStreamAsync(ct);

            var payload = await JsonSerializer.DeserializeAsync<TPayload>(bodyStream, SerializerOptions, ct);

            if (payload is null)
            {
                Logger.EncounteredEmptyResponseBody();

                return Result<TPayload>.Fail("Response is empty");
            }

            return payload;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            Logger.FailedToDeserializeBody(ex);

            return Result<TPayload>.Fail("Failed to deserialize reponse body");
        }
    }
}

internal static partial class BaseClientLogs
{
    [LoggerMessage(
        Level = LogLevel.Error,
        Message = "Exception occurred while sending HTTP request")]
    public static partial void FailedToSend(this ILogger logger, Exception ex);

    [LoggerMessage(
        Level = LogLevel.Error,
        Message = "Exception occurred while deserializing response body")]
    public static partial void FailedToDeserializeBody(this ILogger logger, Exception ex);

    [LoggerMessage(
        Level = LogLevel.Error,
        Message = "Response body is empty")]
    public static partial void EncounteredEmptyResponseBody(this ILogger logger);
}
