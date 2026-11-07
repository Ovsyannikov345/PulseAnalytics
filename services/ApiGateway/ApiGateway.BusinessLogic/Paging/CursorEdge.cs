namespace ApiGateway.BusinessLogic.Paging;

/// <summary>
/// An item of a <see cref="CursorPage{T}"/> and the opaque cursor that points to it.
/// </summary>
public sealed record CursorEdge<T>
{
    public required T Node { get; init; }

    public required string Cursor { get; init; }
}
