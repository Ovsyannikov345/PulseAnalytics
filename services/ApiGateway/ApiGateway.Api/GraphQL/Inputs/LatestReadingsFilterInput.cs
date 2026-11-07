using ApiGateway.Common.Constants.Enums;

namespace ApiGateway.Api.GraphQL.Inputs;

/// <summary>
/// The <c>LatestReadingsFilterInput</c> GraphQL input object; resolvers copy its fields into the service request.
/// </summary>
internal sealed record LatestReadingsFilterInput
{
    public IReadOnlyList<MeterType>? Types { get; init; }

    public IReadOnlyList<MeterLocation>? Locations { get; init; }
}
