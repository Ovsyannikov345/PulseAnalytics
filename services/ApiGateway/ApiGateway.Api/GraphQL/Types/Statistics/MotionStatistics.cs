using HotChocolate.Types;

namespace ApiGateway.Api.GraphQL.Types.Statistics;

internal sealed record MotionStatistics : ReadingStatistics
{
    public required int MotionSamples { get; init; }

    public required double ActivityRatio { get; init; }
}

internal sealed class MotionStatisticsType : ObjectType<MotionStatistics>
{
    protected override void Configure(IObjectTypeDescriptor<MotionStatistics> descriptor)
    {
        descriptor
            .Name("MotionStatistics")
            .Description("Motion activity of one group of motion readings. Each reading is a sample, so activity is counted in samples, not in motion events.");

        ReadingStatisticsFields.Declare(descriptor);

        descriptor
            .Field(s => s.MotionSamples)
            .Description("The number of readings that detected motion.");

        descriptor
            .Field(s => s.ActivityRatio)
            .Description("The share of readings that detected motion, from 0 to 1.");
    }
}
