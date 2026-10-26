using DataIngestor.Application.Correlation;
using Serilog.Core;
using Serilog.Events;

namespace DataIngestor.Api.Logging;

internal sealed class CorrelationIdEnricher : ILogEventEnricher
{
    public const string PropertyName = "CorrelationId";

    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty(PropertyName, CorrelationContext.CorrelationId));
    }
}
