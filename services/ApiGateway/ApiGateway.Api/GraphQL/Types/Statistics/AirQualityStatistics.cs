using HotChocolate.Types;

namespace ApiGateway.Api.GraphQL.Types.Statistics;

internal sealed record AirQualityStatistics : ReadingStatistics
{
    public required MetricStatistics Co2 { get; init; }

    public required MetricStatistics Pm25 { get; init; }

    public required MetricStatistics Humidity { get; init; }
}

internal sealed class AirQualityStatisticsType : ObjectType<AirQualityStatistics>
{
    protected override void Configure(IObjectTypeDescriptor<AirQualityStatistics> descriptor)
    {
        descriptor
            .Name("AirQualityStatistics")
            .Description("Air quality metrics of one group of air quality readings.");

        ReadingStatisticsFields.Declare(descriptor);

        descriptor
            .Field(s => s.Co2)
            .Description("CO2 concentration, ppm.");

        descriptor
            .Field(s => s.Pm25)
            .Description("PM2.5 concentration, µg/m³.");

        descriptor
            .Field(s => s.Humidity)
            .Description("Relative humidity, %.");
    }
}
