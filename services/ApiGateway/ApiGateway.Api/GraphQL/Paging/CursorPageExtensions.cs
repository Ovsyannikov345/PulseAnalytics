using ApiGateway.BusinessLogic.Paging;
using HotChocolate.Types.Pagination;

namespace ApiGateway.Api.GraphQL.Paging;

internal static class CursorPageExtensions
{
    public static Connection<T> ToConnection<T>(this CursorPage<T> page)
    {
        IReadOnlyList<IEdge<T>> edges = [.. page.Edges.Select(edge => new Edge<T>(edge.Node, edge.Cursor))];

        var pageInfo = new ConnectionPageInfo(
            page.HasNextPage,
            page.HasPreviousPage,
            startCursor: page.Edges.Count > 0 ? page.Edges[0].Cursor : null,
            endCursor: page.Edges.Count > 0 ? page.Edges[^1].Cursor : null);

        return new Connection<T>(edges, pageInfo);
    }
}
