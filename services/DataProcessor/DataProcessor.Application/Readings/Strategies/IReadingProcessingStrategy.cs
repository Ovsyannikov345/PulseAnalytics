using Ausiannikau.Utilities.Results;
using DataProcessor.Application.Readings.Models;
using DataProcessor.Domain.Entities;

namespace DataProcessor.Application.Readings.Strategies;

internal interface IReadingProcessingStrategy
{
    public Type ModelType { get; }

    public Result<MeterReading> CreateReading(MeterReadingModel model);
}
