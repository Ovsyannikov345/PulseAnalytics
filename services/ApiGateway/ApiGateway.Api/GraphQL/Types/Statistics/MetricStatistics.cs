using HotChocolate.Types;

namespace ApiGateway.Api.GraphQL.Types.Statistics;

internal sealed record MetricStatistics
{
    public required double Average { get; init; }

    public required double Minimum { get; init; }

    public required double Maximum { get; init; }
}

internal sealed class MetricStatisticsType : ObjectType<MetricStatistics>
{
    protected override void Configure(IObjectTypeDescriptor<MetricStatistics> descriptor)
    {
        descriptor
            .Name("MetricStatistics")
            .Description("Statistics of one measured metric in a group, in the metric's unit.");

        descriptor
            .Field(s => s.Average)
            .Description("Average of the group's values.");

        descriptor
            .Field(s => s.Minimum)
            .Description("Lowest value in the group.");

        descriptor
            .Field(s => s.Maximum)
            .Description("Highest value in the group.");
    }
}
