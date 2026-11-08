using ApiGateway.Common.Constants.Enums;
using HotChocolate.Types;

namespace ApiGateway.Api.GraphQL.Types.Readings;

internal sealed record ReadingCount
{
    public MeterType? Type { get; init; }

    public MeterLocation? Location { get; init; }

    public required int Count { get; init; }
}

internal sealed class ReadingCountType : ObjectType<ReadingCount>
{
    protected override void Configure(IObjectTypeDescriptor<ReadingCount> descriptor)
    {
        descriptor
            .Name("ReadingCount")
            .Description("The number of readings in one group.");

        descriptor
            .Field(c => c.Type)
            .Description("The group's meter type; null unless grouped by TYPE.");

        descriptor
            .Field(c => c.Location)
            .Description("The group's location; null unless grouped by LOCATION.");
    }
}
