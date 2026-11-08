using HotChocolate.Types;

namespace ApiGateway.Api.GraphQL.Types.Statistics;

internal sealed record EnergyStatistics : ReadingStatistics
{
    public required MetricStatistics Energy { get; init; }
}

internal sealed class EnergyStatisticsType : ObjectType<EnergyStatistics>
{
    protected override void Configure(IObjectTypeDescriptor<EnergyStatistics> descriptor)
    {
        descriptor
            .Name("EnergyStatistics")
            .Description("Energy consumption of one group of energy readings.");

        ReadingStatisticsFields.Declare(descriptor);

        descriptor
            .Field(s => s.Energy)
            .Description("Energy consumption reported by the meters.");
    }
}
