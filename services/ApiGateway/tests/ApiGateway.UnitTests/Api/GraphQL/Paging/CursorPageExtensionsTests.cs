using ApiGateway.Api.GraphQL.Paging;
using ApiGateway.BusinessLogic.Paging;
using Shouldly;
using Xunit;

namespace ApiGateway.UnitTests.Api.GraphQL.Paging;

public sealed class CursorPageExtensionsTests
{
    [Fact]
    public void ToConnection_PageWithItems_MapsEdgesAndPageInfo()
    {
        // Arrange
        var page = new CursorPage<string>
        {
            Edges =
            [
                new CursorEdge<string> { Node = "first", Cursor = "c1" },
                new CursorEdge<string> { Node = "second", Cursor = "c2" }
            ],
            HasNextPage = true,
            HasPreviousPage = true
        };

        // Act
        var connection = page.ToConnection();

        // Assert
        connection.Edges.Select(e => (e.Node, e.Cursor)).ShouldBe([("first", "c1"), ("second", "c2")]);
        connection.Info.HasNextPage.ShouldBeTrue();
        connection.Info.HasPreviousPage.ShouldBeTrue();
        connection.Info.StartCursor.ShouldBe("c1");
        connection.Info.EndCursor.ShouldBe("c2");
    }

    [Fact]
    public void ToConnection_EmptyPage_HasNoCursors()
    {
        // Arrange
        var page = new CursorPage<string> { Edges = [], HasNextPage = false, HasPreviousPage = false };

        // Act
        var connection = page.ToConnection();

        // Assert
        connection.Edges.ShouldBeEmpty();
        connection.Info.StartCursor.ShouldBeNull();
        connection.Info.EndCursor.ShouldBeNull();
    }
}
