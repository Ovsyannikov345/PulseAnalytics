using ApiGateway.Common.Constants.Enums;
using HotChocolate.Types;

namespace ApiGateway.Api.GraphQL.Inputs;

internal sealed record LatestReadingsFilterInput
{
    public IReadOnlyList<MeterType>? Types { get; init; }

    public IReadOnlyList<MeterLocation>? Locations { get; init; }
}

internal sealed class LatestReadingsFilterInputType : InputObjectType<LatestReadingsFilterInput>
{
    protected override void Configure(IInputObjectTypeDescriptor<LatestReadingsFilterInput> descriptor)
    {
        descriptor
            .Name("LatestReadingsFilterInput")
            .Description("Selects meters by type and location. Omitted or empty fields don't filter.");

        descriptor
            .Field(f => f.Types)
            .Description("Meter types to include.");

        descriptor
            .Field(f => f.Locations)
            .Description("Locations to include.");
    }
}
