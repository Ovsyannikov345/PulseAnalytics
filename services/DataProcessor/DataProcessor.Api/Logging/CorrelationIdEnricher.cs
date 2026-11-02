using DataProcessor.Application.Correlation;
using Serilog.Core;
using Serilog.Events;

namespace DataProcessor.Api.Logging;

internal sealed class CorrelationIdEnricher : ILogEventEnricher
{
    public const string PropertyName = "CorrelationId";

    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty(PropertyName, CorrelationContext.CorrelationId));
    }
}
