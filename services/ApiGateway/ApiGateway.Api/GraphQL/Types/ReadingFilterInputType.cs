using ApiGateway.Api.GraphQL.Inputs;
using HotChocolate.Types;

namespace ApiGateway.Api.GraphQL.Types;

internal sealed class ReadingFilterInputType : InputObjectType<ReadingFilterInput>
{
    protected override void Configure(IInputObjectTypeDescriptor<ReadingFilterInput> descriptor)
    {
        descriptor
            .Name("ReadingFilterInput")
            .Description("Selects readings by meter and time. Omitted or empty fields don't filter.");

        descriptor
            .Field(f => f.Types)
            .Description("Meter types to include.");

        descriptor
            .Field(f => f.Locations)
            .Description("Locations to include.");

        descriptor
            .Field(f => f.From)
            .Description("Inclusive start of the time range.");

        descriptor
            .Field(f => f.To)
            .Description("Exclusive end of the time range; must be later than `from`.");
    }
}
