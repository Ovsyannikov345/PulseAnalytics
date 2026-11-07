using ApiGateway.BusinessLogic.MeterReadings.Models;
using ApiGateway.Common.Constants.Enums;
using ApiGateway.DataAccess.Entities;

namespace ApiGateway.BusinessLogic.MeterReadings.Mapping;

internal static class MeterReadingMappings
{
    public static MeterReadingModel ToModel(this MeterReading reading)
    {
        return reading.Type switch
        {
            MeterType.Motion => new MotionReadingModel
            {
                Id = reading.Id,
                Location = reading.Location,
                ReceivedAt = reading.ReceivedAt,
                MotionDetected = GetValueOrThrow(reading, reading.MotionDetected, nameof(MeterReading.MotionDetected))
            },
            MeterType.Energy => new EnergyReadingModel
            {
                Id = reading.Id,
                Location = reading.Location,
                ReceivedAt = reading.ReceivedAt,
                Energy = GetValueOrThrow(reading, reading.Energy, nameof(MeterReading.Energy))
            },
            MeterType.AirQuality => new AirQualityReadingModel
            {
                Id = reading.Id,
                Location = reading.Location,
                ReceivedAt = reading.ReceivedAt,
                Co2 = GetValueOrThrow(reading, reading.Co2, nameof(MeterReading.Co2)),
                Pm25 = GetValueOrThrow(reading, reading.Pm25, nameof(MeterReading.Pm25)),
                Humidity = GetValueOrThrow(reading, reading.Humidity, nameof(MeterReading.Humidity))
            },
            _ => throw new InvalidOperationException($"Meter reading {reading.Id} has an unknown type {reading.Type}.")
        };
    }

    private static TValue GetValueOrThrow<TValue>(MeterReading reading, TValue? value, string propertyName)
        where TValue : struct
    {
        return value ?? throw new InvalidOperationException($"{reading.Type} reading {reading.Id} has no {propertyName} value.");
    }
}
