using ApiGateway.BusinessLogic.MeterReadings.Models;
using HotChocolate.Types;

namespace ApiGateway.Api.GraphQL.Types;

internal sealed class MeterReadingInterfaceType : InterfaceType<MeterReadingModel>
{
    protected override void Configure(IInterfaceTypeDescriptor<MeterReadingModel> descriptor)
    {
        descriptor
            .Name("MeterReading")
            .Description("A single sample of one meter. The concrete type carries the measured values.");

        MeterReadingFields.Declare(descriptor);
    }
}
