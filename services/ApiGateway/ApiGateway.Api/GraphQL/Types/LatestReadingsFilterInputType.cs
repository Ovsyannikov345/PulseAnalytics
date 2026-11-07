using ApiGateway.Api.GraphQL.Inputs;
using HotChocolate.Types;

namespace ApiGateway.Api.GraphQL.Types;

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
