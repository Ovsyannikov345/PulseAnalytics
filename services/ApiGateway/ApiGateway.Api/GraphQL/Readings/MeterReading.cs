using ApiGateway.Common.Constants.Enums;
using HotChocolate.Types;

namespace ApiGateway.Api.GraphQL.Readings;

internal abstract record MeterReading
{
    public required Guid Id { get; init; }

    public abstract MeterType Type { get; }

    public required MeterLocation Location { get; init; }

    public required DateTimeOffset ReceivedAt { get; init; }
}

internal static class MeterReadingFields
{
    private const string IdDescription = "Unique id of the reading.";

    private const string ReceivedAtDescription = "When the ingestor received the sample (UTC).";

    public static void Declare(IInterfaceTypeDescriptor<MeterReading> descriptor)
    {
        descriptor.Field(r => r.Id).Description(IdDescription);
        descriptor.Field(r => r.Type);
        descriptor.Field(r => r.Location);
        descriptor.Field(r => r.ReceivedAt).Description(ReceivedAtDescription);
    }

    public static void Declare<TReading>(IObjectTypeDescriptor<TReading> descriptor)
        where TReading : MeterReading
    {
        descriptor.Field(r => r.Id).Description(IdDescription);
        descriptor.Field(r => r.Type);
        descriptor.Field(r => r.Location);
        descriptor.Field(r => r.ReceivedAt).Description(ReceivedAtDescription);
    }
}

internal sealed class MeterReadingInterfaceType : InterfaceType<MeterReading>
{
    protected override void Configure(IInterfaceTypeDescriptor<MeterReading> descriptor)
    {
        descriptor
            .Name("MeterReading")
            .Description("A single sample of one meter. The concrete type carries the measured values.");

        MeterReadingFields.Declare(descriptor);
    }
}
