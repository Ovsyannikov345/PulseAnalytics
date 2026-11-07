using ApiGateway.Common.Constants.Enums;

namespace ApiGateway.BusinessLogic.MeterReadings.Models;

public sealed record EnergyReadingModel : MeterReadingModel
{
    public override MeterType Type => MeterType.Energy;

    public required double Energy { get; init; }
}
