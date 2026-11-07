using ApiGateway.BusinessLogic.MeterReadings.Models;
using HotChocolate.Types;

namespace ApiGateway.Api.GraphQL.Types;

internal sealed class ReadingCountType : ObjectType<ReadingCountModel>
{
    protected override void Configure(IObjectTypeDescriptor<ReadingCountModel> descriptor)
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
