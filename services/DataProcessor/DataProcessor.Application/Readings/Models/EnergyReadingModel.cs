namespace DataProcessor.Application.Readings.Models;

public sealed record EnergyReadingModel : MeterReadingModel
{
    public required double Energy { get; init; }
}
