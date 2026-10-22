using Ausiannikau.Utilities.Results;
using DataIngestor.Application.Interfaces.Clients;
using DataIngestor.Domain.Constants.Enums;
using DataIngestor.Domain.Entities;
using DataIngestor.Infrastructure.WeakApp.Contracts;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DataIngestor.Infrastructure.WeakApp.Clients;

internal sealed class WeakAppClient(HttpClient httpClient, ILogger<WeakAppClient> logger)
    : BaseClient(httpClient, logger), IExternalMetersClient
{
    protected override JsonSerializerOptions SerializerOptions => new JsonSerializerOptions
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public async Task<Result<List<MeterReading>>> GetMetersAsync(CancellationToken ct)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "meters");

        var requestResult = await SendAsync(request, ct);

        if (!requestResult.TryGetSuccessValue(out var response))
        {
            return requestResult.ToFailureOf<List<MeterReading>>();
        }

        if (!response.IsSuccessStatusCode)
        {
            return Result<List<MeterReading>>.Fail("Weak App API responded with failure");
        }

        var utcNow = DateTimeOffset.UtcNow;

        using (response)
        {
            var deserializationResult = await DeserializeAsync<List<MeterReadingResponse>>(response, ct);

            if (!deserializationResult.TryGetSuccessValue(out var readings))
            {
                return Result<List<MeterReading>>.Fail("Failed to read Weak App API response");
            }

            return MapResponse(readings);
        }

        List<MeterReading> MapResponse(List<MeterReadingResponse> readings)
        {
            var mappedReadings = new List<MeterReading>(readings.Count);

            foreach (var reading in readings)
            {
                if (reading is MotionMeterReadingResponse motionReading)
                {
                    mappedReadings.Add(new MeterReading
                    {
                        Name = motionReading.Name,
                        Type = MeterType.Motion,
                        ReceivedAt = utcNow,
                        Payload = new MotionPayload { MotionDetected = motionReading.Payload.MotionDetected }
                    });
                }

                if (reading is EnergyMeterReadingResponse energyReading)
                {
                    mappedReadings.Add(new MeterReading
                    {
                        Name = energyReading.Name,
                        Type = MeterType.Energy,
                        ReceivedAt = utcNow,
                        Payload = new EnergyPayload { Energy = energyReading.Payload.Energy }
                    });
                }

                if (reading is AirQualityMeterReadingResponse airReading)
                {
                    mappedReadings.Add(new MeterReading
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
                    });
                }

                Logger.UnknownReadingType(reading.GetType());
            }

            return mappedReadings;
        }
    }
}

public static partial class WeakAppClientLogs
{
    [LoggerMessage(Level = LogLevel.Warning, Message = "Unknown meter reading type encountered: {Type}")]
    public static partial void UnknownReadingType(this ILogger logger, Type type);
}
