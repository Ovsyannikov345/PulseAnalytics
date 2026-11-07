using ApiGateway.BusinessLogic.MeterReadings.Models;
using HotChocolate.Types;

namespace ApiGateway.Api.GraphQL.Types;

/// <summary>
/// Declares the fields of the <c>MeterReading</c> interface first and in the same order on every reading type,
/// so the type-specific values follow the shared ones in the schema.
/// </summary>
internal static class MeterReadingFields
{
    private const string IdDescription = "Unique id of the reading.";
    private const string ReceivedAtDescription = "When the ingestor received the sample (UTC).";

    public static void Declare(IInterfaceTypeDescriptor<MeterReadingModel> descriptor)
    {
        descriptor.Field(r => r.Id).Description(IdDescription);
        descriptor.Field(r => r.Type);
        descriptor.Field(r => r.Location);
        descriptor.Field(r => r.ReceivedAt).Description(ReceivedAtDescription);
    }

    public static void Declare<TModel>(IObjectTypeDescriptor<TModel> descriptor)
        where TModel : MeterReadingModel
    {
        descriptor.Field(r => r.Id).Description(IdDescription);
        descriptor.Field(r => r.Type);
        descriptor.Field(r => r.Location);
        descriptor.Field(r => r.ReceivedAt).Description(ReceivedAtDescription);
    }
}
