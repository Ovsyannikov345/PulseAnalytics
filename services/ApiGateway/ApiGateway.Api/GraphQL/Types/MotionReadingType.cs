using ApiGateway.BusinessLogic.MeterReadings.Models;
using HotChocolate.Types;

namespace ApiGateway.Api.GraphQL.Types;

internal sealed class MotionReadingType : ObjectType<MotionReadingModel>
{
    protected override void Configure(IObjectTypeDescriptor<MotionReadingModel> descriptor)
    {
        descriptor
            .Name("MotionReading")
            .Implements<MeterReadingInterfaceType>();

        MeterReadingFields.Declare(descriptor);

        descriptor
            .Field(r => r.MotionDetected)
            .Description("Whether the sensor detected motion at the time of the sample.");
    }
}
