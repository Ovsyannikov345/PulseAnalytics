using System.ComponentModel;
using ApiGateway.Common.Constants.Enums;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace ApiGateway.Api.Rest.Models;
 
public sealed record ExportReadingsQuery
{
    [BindRequired]
    [Description("Start of the range (inclusive), ISO 8601 with an offset, e.g. 2026-11-01T00:00:00Z.")]
    public required DateTimeOffset From { get; init; }

    [BindRequired]
    [Description("End of the range (exclusive), at most 7 days after 'from'.")]
    public required DateTimeOffset To { get; init; }

    [Description("Reading types to include (repeat the parameter for several); all when omitted.")]
    public MeterType[]? Types { get; init; }

    [Description("Locations to include (repeat the parameter for several); all when omitted.")]
    public MeterLocation[]? Locations { get; init; }
}
