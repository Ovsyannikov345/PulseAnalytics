using Ausiannikau.Utilities.Results;
using DataProcessor.Application.Readings.Models;
using DataProcessor.Domain.Constants.Enums;
using DataProcessor.Domain.Entities;
using DataProcessor.Domain.Entities.Payloads;

namespace DataProcessor.Application.Readings.Strategies;

internal abstract class ReadingProcessingStrategy<TModel> : IReadingProcessingStrategy
    where TModel : MeterReadingModel
{
    public Type ModelType => typeof(TModel);

    protected abstract MeterType MeterType { get; }

    public Result<MeterReading> CreateReading(MeterReadingModel model)
    {
        if (model is not TModel typedModel)
        {
            return Result<MeterReading>.Fail($"{GetType().Name} cannot process a {model.GetType().Name}");
        }

        return Result<MeterReading>.Succeed(new MeterReading
        {
            DeduplicationKey = typedModel.DeduplicationKey,
            Type = MeterType,
            Location = typedModel.Location,
            ReceivedAt = typedModel.ReceivedAt,
            Payload = CreatePayload(typedModel)
        });
    }

    protected abstract ReadingPayload CreatePayload(TModel model);
}
