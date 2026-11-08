using ApiGateway.BusinessLogic.Statistics;
using ApiGateway.Common.Constants.Enums;
using HotChocolate.Types;

namespace ApiGateway.Api.GraphQL.Inputs;

internal sealed record StatisticsFilterInput
{
    public IReadOnlyList<MeterLocation>? Locations { get; init; }

    public required DateTimeOffset From { get; init; }

    public required DateTimeOffset To { get; init; }
}

internal sealed class StatisticsFilterInputType : InputObjectType<StatisticsFilterInput>
{
    protected override void Configure(IInputObjectTypeDescriptor<StatisticsFilterInput> descriptor)
    {
        descriptor
            .Name("StatisticsFilterInput")
            .Description($"Selects the readings to aggregate. The time range is required and spans at most {StatisticsLimits.MaxRange.Days} days.");

        descriptor
            .Field(f => f.Locations)
            .Description("Locations to include; omitted or empty includes all.");

        descriptor
            .Field(f => f.From)
            .Description("Inclusive start of the time range.");

        descriptor
            .Field(f => f.To)
            .Description("Exclusive end of the time range; must be later than `from`.");
    }
}
