namespace ApiGateway.BusinessLogic.Paging;

public sealed record CursorPage<T>
{
    public required IReadOnlyList<CursorPageItem<T>> Items { get; init; }

    public required bool HasNextPage { get; init; }

    public required bool HasPreviousPage { get; init; }
}
