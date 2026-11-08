using ApiGateway.Common.Constants.Enums;
using HotChocolate.Types;

namespace ApiGateway.Api.GraphQL.Types.Readings;

internal sealed record MotionReading : MeterReading
{
    public override MeterType Type => MeterType.Motion;

    public required bool MotionDetected { get; init; }
}

internal sealed class MotionReadingType : ObjectType<MotionReading>
{
    protected override void Configure(IObjectTypeDescriptor<MotionReading> descriptor)
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
