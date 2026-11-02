using DataProcessor.Application.MeterReadings.Models;

namespace DataProcessor.Application.MeterReadings.Validators;

internal sealed class MotionReadingModelValidator(TimeProvider timeProvider)
    : MeterReadingModelValidator<MotionReadingModel>(timeProvider);
