using ApiGateway.Common.Constants.Enums;
using HotChocolate.Types;

namespace ApiGateway.Api.GraphQL.Types.Readings;

internal sealed record AirQualityReading : MeterReading
{
    public override MeterType Type => MeterType.AirQuality;

    public required int Co2 { get; init; }

    public required int Pm25 { get; init; }

    public required int Humidity { get; init; }
}

internal sealed class AirQualityReadingType : ObjectType<AirQualityReading>
{
    protected override void Configure(IObjectTypeDescriptor<AirQualityReading> descriptor)
    {
        descriptor
            .Name("AirQualityReading")
            .Implements<MeterReadingInterfaceType>();

        MeterReadingFields.Declare(descriptor);

        descriptor
            .Field(r => r.Co2)
            .Description("CO2 concentration, ppm.");

        descriptor
            .Field(r => r.Pm25)
            .Description("PM2.5 concentration, µg/m³.");

        descriptor
            .Field(r => r.Humidity)
            .Description("Relative humidity, %.");
    }
}
