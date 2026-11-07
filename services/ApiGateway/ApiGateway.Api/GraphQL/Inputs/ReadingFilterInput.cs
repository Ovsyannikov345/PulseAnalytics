using ApiGateway.Common.Constants.Enums;

namespace ApiGateway.Api.GraphQL.Inputs;

/// <summary>
/// The <c>ReadingFilterInput</c> GraphQL input object; resolvers copy its fields into the service request.
/// </summary>
internal sealed record ReadingFilterInput
{
    public IReadOnlyList<MeterType>? Types { get; init; }

    public IReadOnlyList<MeterLocation>? Locations { get; init; }

    public DateTimeOffset? From { get; init; }

    public DateTimeOffset? To { get; init; }
}
