using ApiGateway.BusinessLogic.MeterReadings.Models;
using HotChocolate.Types;

namespace ApiGateway.Api.GraphQL.Types;

internal sealed class EnergyReadingType : ObjectType<EnergyReadingModel>
{
    protected override void Configure(IObjectTypeDescriptor<EnergyReadingModel> descriptor)
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
