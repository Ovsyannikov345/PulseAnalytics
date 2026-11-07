using ApiGateway.Common.Constants.Enums;
using HotChocolate.Types;

namespace ApiGateway.Api.GraphQL.Readings;

internal sealed record EnergyReading : MeterReading
{
    public override MeterType Type => MeterType.Energy;

    public required double Energy { get; init; }
}

internal sealed class EnergyReadingType : ObjectType<EnergyReading>
{
    protected override void Configure(IObjectTypeDescriptor<EnergyReading> descriptor)
    {
        descriptor
            .Name("EnergyReading")
            .Implements<MeterReadingInterfaceType>();

        MeterReadingFields.Declare(descriptor);

        descriptor
            .Field(r => r.Energy)
            .Description("Energy consumption reported by the meter.");
    }
}
