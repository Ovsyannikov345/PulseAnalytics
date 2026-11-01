namespace DataProcessor.Application.MeterReadings.Models;

public sealed record EnergyReadingModel : MeterReadingModel
{
    public required double Energy { get; init; }
}
