using System.Text.Json;
using Ausiannikau.Utilities.Results;
using DataIngestor.Application.Interfaces.Clients;
using DataIngestor.Domain.Constants.Enums;
using DataIngestor.Domain.Entities;
using DataIngestor.Infrastructure.Abstractions;
using DataIngestor.Infrastructure.WeakApp.Contracts;
using Microsoft.Extensions.Logging;

namespace DataIngestor.Infrastructure.WeakApp.Clients;

internal sealed class WeakAppClient(HttpClient httpClient, ILogger<WeakAppClient> logger)
    : BaseHttpClient(httpClient, logger, SerializerOptions), IExternalMetersClient
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        AllowOutOfOrderMetadataProperties = true,
        RespectNullableAnnotations = true
    };

    public async Task<Result<List<MeterReading>>> GetMetersAsync(CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "meters");

        var requestResult = await SendAsync(request, ct);

        if (!requestResult.TryGetSuccessValue(out var response))
        {
            return requestResult.ToFailureOf<List<MeterReading>>();
        }

        var utcNow = DateTimeOffset.UtcNow;

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                await LogFailedResponseAsync(response, ct);

                return Result<List<MeterReading>>.Fail("Weak App API responded with failure");
            }

            var deserializationResult = await DeserializeAsync<List<JsonElement>>(response, ct);

            if (!deserializationResult.TryGetSuccessValue(out var rawReadings))
            {
                return Result<List<MeterReading>>.Fail("Failed to read Weak App API response");
            }

            var parsedReadings = ParseReadings(rawReadings);

            return MapResponse(parsedReadings);
        }

        List<MeterReading> MapResponse(List<MeterReadingResponse> readings)
        {
            var mappedReadings = new List<MeterReading>(readings.Count);

            foreach (var reading in readings)
            {
                var mappedReading = reading switch
                {
                    MotionMeterReadingResponse motionReading => new MeterReading
                    {
                        Name = motionReading.Name,
                        Type = MeterType.Motion,
                        ReceivedAt = utcNow,
                        Payload = new MotionPayload { MotionDetected = motionReading.Payload.MotionDetected }
                    },

                    EnergyMeterReadingResponse energyReading => new MeterReading
                    {
                        Name = energyReading.Name,
                        Type = MeterType.Energy,
                        ReceivedAt = utcNow,
                        Payload = new EnergyPayload { Energy = energyReading.Payload.Energy }
                    },

                    AirQualityMeterReadingResponse airReading => new MeterReading
                    {
                        Name = airReading.Name,
                        Type = MeterType.AirQuality,
                        ReceivedAt = utcNow,
                        Payload = new AirQualityPayload
                        {
                            Co2 = airReading.Payload.Co2,
                            Pm25 = airReading.Payload.Pm25,
                            Humidity = airReading.Payload.Humidity
                        }
                    },

                    _ => null
                };

                if (mappedReading is not null)
                {
                    mappedReadings.Add(mappedReading);
                }
                else
                {
                    Logger.UnknownReadingType(reading.GetType());
                }
            }

            return mappedReadings;
        }
    }

    private List<MeterReadingResponse> ParseReadings(List<JsonElement> items)
    {
        var readings = new List<MeterReadingResponse>(items.Count);

        for (var index = 0; index < items.Count; index++)
        {
            try
            {
                var reading = items[index].Deserialize<MeterReadingResponse>(SerializerOptions);

                if (reading is not null)
                {
                    readings.Add(reading);
                }
                else
                {
                    Logger.SkippedNullReading(index);
                }
            }
            catch (Exception ex)
            {
                Logger.SkippedMalformedReading(index, ex);
            }
        }

        return readings;
    }
}

internal static partial class WeakAppClientLogs
{
    [LoggerMessage(Level = LogLevel.Warning, Message = "Unknown meter reading type encountered: {Type}")]
    public static partial void UnknownReadingType(this ILogger logger, Type type);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Skipped malformed meter reading at index {Index}")]
    public static partial void SkippedMalformedReading(this ILogger logger, int index, Exception ex);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Skipped null meter reading at index {Index}")]
    public static partial void SkippedNullReading(this ILogger logger, int index);
}
