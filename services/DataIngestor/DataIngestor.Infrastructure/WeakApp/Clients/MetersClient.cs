using Ausiannikau.Utilities.Results;
using DataIngestor.Application.ExternalMeters.Dto;
using DataIngestor.Application.Interfaces.Clients;
using DataIngestor.Infrastructure.WeakApp.Contracts;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DataIngestor.Infrastructure.WeakApp.Clients;

internal sealed class MetersClient(HttpClient httpClient, ILogger<MetersClient> logger)
    : BaseClient(httpClient, logger), IMetersClient
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
            return Result<List<MeterReading>>.Fail("Meters API responded with failure");
        }

        using (response)
        {
            var deserializationResult = await DeserializeAsync<List<MeterReadingResponse>>(response, ct);

            if (!deserializationResult.TryGetSuccessValue(out var readings))
            {
                return Result<List<MeterReading>>.Fail("Failed to read response");
            }

            return MapResults(readings);
        }

        static Result<List<MeterReading>> MapResults(List<MeterReadingResponse> readings)
        {
            var mapped = new List<MeterReading>(readings.Count);

            foreach (var r in readings)
            {
                MeterReading? reading = r switch
                {
                    MotionMeterReadingResponse motionReading => new MotionMeterReading
                    {
                        Name = motionReading.Name,
                        Payload = new MotionPayload { MotionDetected = motionReading.Payload.MotionDetected }
                    },
                    EnergyMeterReadingResponse energyReading => new EnergyMeterReading
                    {
                        Name = energyReading.Name,
                        Payload = new EnergyPayload { Energy = energyReading.Payload.Energy }
                    },
                    AirQualityMeterReadingResponse airReading => new AirQualityMeterReading
                    {
                        Name = airReading.Name,
                        Payload = new AirQualityPayload
                        {
                            Co2 = airReading.Payload.Co2,
                            Pm25 = airReading.Payload.Pm25,
                            Humidity = airReading.Payload.Humidity
                        }
                    },
                    _ => null
                };

                if (reading is null)
                {
                    return Result<List<MeterReading>>.Fail("Unsupported meter reading type encountered");
                }

                mapped.Add(reading);
            }

            return mapped;
        }
    }
}
