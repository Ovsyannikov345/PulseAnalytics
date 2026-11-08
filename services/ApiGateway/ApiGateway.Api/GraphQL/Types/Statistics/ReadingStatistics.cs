using ApiGateway.Common.Constants.Enums;
using HotChocolate.Types;

namespace ApiGateway.Api.GraphQL.Types.Statistics;

internal abstract record ReadingStatistics
{
    public MeterLocation? Location { get; init; }

    public DateTimeOffset? BucketStart { get; init; }

    public required int Samples { get; init; }
}

internal static class ReadingStatisticsFields
{
    private const string LocationDescription = "The group's location; null unless grouped by LOCATION.";

    private const string BucketStartDescription = "Start of the group's time bucket (UTC); null unless grouped by TIME.";

    private const string SamplesDescription = "The number of readings in the group.";

    public static void Declare<TStatistics>(IObjectTypeDescriptor<TStatistics> descriptor)
        where TStatistics : ReadingStatistics
    {
        descriptor.Field(s => s.Location).Description(LocationDescription);
        descriptor.Field(s => s.BucketStart).Description(BucketStartDescription);
        descriptor.Field(s => s.Samples).Description(SamplesDescription);
    }
}
