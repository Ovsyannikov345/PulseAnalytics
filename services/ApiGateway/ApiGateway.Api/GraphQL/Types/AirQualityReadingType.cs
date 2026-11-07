using ApiGateway.BusinessLogic.MeterReadings.Models;
using HotChocolate.Types;

namespace ApiGateway.Api.GraphQL.Types;

internal sealed class AirQualityReadingType : ObjectType<AirQualityReadingModel>
{
    protected override void Configure(IObjectTypeDescriptor<AirQualityReadingModel> descriptor)
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
