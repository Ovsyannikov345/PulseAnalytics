using Ausiannikau.Utilities.Results;
using DataIngestor.Contracts.Events;
using DataProcessor.Application.MeterReadings.Models;
using DataProcessor.Domain.Constants.Enums;

namespace DataProcessor.Infrastructure.RabbitMq.Mappers;

internal static class MeterReadingMessageMapper
{
    private static readonly Dictionary<string, MeterLocation> Locations = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Kitchen"] = MeterLocation.Kitchen,
        ["Living Room"] = MeterLocation.LivingRoom,
        ["Bedroom"] = MeterLocation.Bedroom,
        ["Garage"] = MeterLocation.Garage,
        ["Office"] = MeterLocation.Office,
        ["Corridor"] = MeterLocation.Corridor
    };

    public static Result<MeterReadingModel> ToModel(MotionReadingReceived message)
    {
        var locationResult = ParseLocation(message.Location);

        if (!locationResult.TryGetSuccessValue(out var location))
        {
            return locationResult.ToFailureOf<MeterReadingModel>();
        }

        return Result<MeterReadingModel>.Succeed(new MotionReadingModel
        {
            DeduplicationKey = message.MessageId,
            Location = location,
            ReceivedAt = message.ReceivedAt,
            MotionDetected = message.MotionDetected
        });
    }

    public static Result<MeterReadingModel> ToModel(EnergyReadingReceived message)
    {
        var locationResult = ParseLocation(message.Location);

        if (!locationResult.TryGetSuccessValue(out var location))
        {
            return locationResult.ToFailureOf<MeterReadingModel>();
        }

        return Result<MeterReadingModel>.Succeed(new EnergyReadingModel
        {
            DeduplicationKey = message.MessageId,
            Location = location,
            ReceivedAt = message.ReceivedAt,
            Energy = message.Energy
        });
    }

    public static Result<MeterReadingModel> ToModel(AirQualityReadingReceived message)
    {
        var locationResult = ParseLocation(message.Location);

        if (!locationResult.TryGetSuccessValue(out var location))
        {
            return locationResult.ToFailureOf<MeterReadingModel>();
        }

        return Result<MeterReadingModel>.Succeed(new AirQualityReadingModel
        {
            DeduplicationKey = message.MessageId,
            Location = location,
            ReceivedAt = message.ReceivedAt,
            Co2 = message.Co2,
            Pm25 = message.Pm25,
            Humidity = message.Humidity
        });
    }

    private static Result<MeterLocation> ParseLocation(string? name)
    {
        return name is not null && Locations.TryGetValue(name, out var location)
            ? Result<MeterLocation>.Succeed(location)
            : Result<MeterLocation>.Fail($"Unknown location '{name}'");
    }
}
