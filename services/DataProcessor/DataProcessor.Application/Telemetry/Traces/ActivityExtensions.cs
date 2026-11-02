using System.Diagnostics;

namespace DataProcessor.Application.Telemetry.Traces;

public static class ActivityExtensions
{
    public static void MarkFailed(this Activity? activity, string reason)
    {
        activity?.SetStatus(ActivityStatusCode.Error, reason);
    }

    public static void MarkFailed(this Activity? activity, Exception exception)
    {
        activity?.AddException(exception);
        activity?.SetTag(ProcessorTelemetry.Tags.ErrorType, exception.GetType().FullName);
        activity?.SetStatus(ActivityStatusCode.Error, exception.Message);
    }
}
