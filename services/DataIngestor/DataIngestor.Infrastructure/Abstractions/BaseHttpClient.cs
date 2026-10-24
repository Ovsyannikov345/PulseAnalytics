using System.Net;
using System.Text.Json;
using Ausiannikau.Utilities.Results;
using Microsoft.Extensions.Logging;

namespace DataIngestor.Infrastructure.Abstractions;

internal abstract class BaseHttpClient(HttpClient httpClient, ILogger logger, JsonSerializerOptions serializerOptions)
{
    protected ILogger Logger => logger;

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

            var payload = await JsonSerializer.DeserializeAsync<TPayload>(bodyStream, serializerOptions, ct);

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

            return Result<TPayload>.Fail("Failed to deserialize response body");
        }
    }

    protected async Task LogFailedResponseAsync(HttpResponseMessage response, CancellationToken ct)
    {
        var request = response.RequestMessage;

        await response.Content.LoadIntoBufferAsync(ct);
        var responseBody = await ReadBodySafelyAsync();

        Logger.ApiRespondedWithFailure(
            request?.Method,
            request?.RequestUri,
            (int)response.StatusCode,
            responseBody);

        async Task<string?> ReadBodySafelyAsync()
        {
            try
            {
                return await response.Content.ReadAsStringAsync(CancellationToken.None);
            }
            catch (Exception ex)
            {
                Logger.FailedToReadResponseBody(ex);

                return "[Failed to read]";
            }
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

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "API responded with failure. HTTP method: {HttpMethod}. URL: {RequestUri}. " +
            "Status code: {ResponseStatusCode}. Response body: {ResponseBody}")]
    public static partial void ApiRespondedWithFailure(
        this ILogger logger,
        HttpMethod? httpMethod,
        Uri? requestUri,
        int responseStatusCode,
        string? responseBody);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Failed to read response body.")]
    public static partial void FailedToReadResponseBody(this ILogger logger, Exception ex);
}
