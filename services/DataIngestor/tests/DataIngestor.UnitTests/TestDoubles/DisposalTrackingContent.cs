namespace DataIngestor.UnitTests.TestDoubles;

/// <summary>
/// String content that records whether it was disposed. Disposing a response disposes its content,
/// so this shows whether the code under test released the response (and its connection).
/// </summary>
internal sealed class DisposalTrackingContent(string content) : StringContent(content)
{
    public bool IsDisposed { get; private set; }

    protected override void Dispose(bool disposing)
    {
        IsDisposed = true;

        base.Dispose(disposing);
    }
}
