using Ausiannikau.Utilities.Results;
using DataProcessor.Contracts.Events;
using NotificationService.Application.LiveReadings.Enums;
using NotificationService.Application.LiveReadings.Models;
using ContractMeterLocation = DataProcessor.Contracts.Enums.MeterLocation;

namespace NotificationService.Infrastructure.RabbitMq.Mappers;

internal static class LiveReadingMessageMapper
{
    public static Result<LiveReadingModel> ToModel(MotionReadingStored message)
    {
        var locationResult = MapLocation(message.Location);

        if (!locationResult.TryGetSuccessValue(out var location))
        {
            return locationResult.ToFailureOf<LiveReadingModel>();
        }

        return Result<LiveReadingModel>.Succeed(new MotionLiveReadingModel
        {
            Id = message.ReadingId,
            Location = location,
            ReceivedAt = message.ReceivedAt,
            MotionDetected = message.MotionDetected,
        });
    }

    public static Result<LiveReadingModel> ToModel(EnergyReadingStored message)
    {
        var locationResult = MapLocation(message.Location);

        if (!locationResult.TryGetSuccessValue(out var location))
        {
            return locationResult.ToFailureOf<LiveReadingModel>();
        }

        return Result<LiveReadingModel>.Succeed(new EnergyLiveReadingModel
        {
            Id = message.ReadingId,
            Location = location,
            ReceivedAt = message.ReceivedAt,
            Energy = message.Energy,
        });
    }

    public static Result<LiveReadingModel> ToModel(AirQualityReadingStored message)
    {
        var locationResult = MapLocation(message.Location);

        if (!locationResult.TryGetSuccessValue(out var location))
        {
            return locationResult.ToFailureOf<LiveReadingModel>();
        }

        return Result<LiveReadingModel>.Succeed(new AirQualityLiveReadingModel
        {
            Id = message.ReadingId,
            Location = location,
            ReceivedAt = message.ReceivedAt,
            Co2 = message.Co2,
            Pm25 = message.Pm25,
            Humidity = message.Humidity,
        });
    }

    // The contract's enum is read by name, but an integer outside the known values deserializes too.
    private static Result<MeterLocation> MapLocation(ContractMeterLocation location)
    {
        return location switch
        {
            ContractMeterLocation.Kitchen => Result<MeterLocation>.Succeed(MeterLocation.Kitchen),
            ContractMeterLocation.LivingRoom => Result<MeterLocation>.Succeed(MeterLocation.LivingRoom),
            ContractMeterLocation.Bedroom => Result<MeterLocation>.Succeed(MeterLocation.Bedroom),
            ContractMeterLocation.Garage => Result<MeterLocation>.Succeed(MeterLocation.Garage),
            ContractMeterLocation.Office => Result<MeterLocation>.Succeed(MeterLocation.Office),
            ContractMeterLocation.Corridor => Result<MeterLocation>.Succeed(MeterLocation.Corridor),
            _ => Result<MeterLocation>.Fail($"Unknown location '{location}'"),
        };
    }
}
