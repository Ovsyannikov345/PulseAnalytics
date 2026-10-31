namespace DataProcessor.Application.Correlation;

/// <summary>
/// Holds the correlation ID of the current execution flow.
/// </summary>
public static class CorrelationContext
{
    private static readonly AsyncLocal<Guid?> Current = new();

    public static Guid CorrelationId
    {
        get => Current.Value ??= Guid.CreateVersion7();
        set => Current.Value = value;
    }

    public static void Reset()
    {
        Current.Value = null;
    }
}
