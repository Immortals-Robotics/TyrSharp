using System.Numerics;
using Tyr.Common.Math;
using Tyr.Common.Math.Shapes;

namespace Tyr.Tests.Common.Math;

public partial class GeometryTests
{
    [Fact]
    public void HasIntersection_OverlappingRectangles_ReturnsTrue()
    {
        // Arrange
        var rect1 = new Rect(new Vector2(0, 0), new Vector2(5, 5));
        var rect2 = new Rect(new Vector2(3, 3), new Vector2(8, 8));

        // Act
        var result = Geometry.HasIntersection(rect1, rect2);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void HasIntersection_TouchingRectangles_ReturnsTrue()
    {
        // Arrange
        var rect1 = new Rect(new Vector2(0, 0), new Vector2(5, 5));
        var rect2 = new Rect(new Vector2(5, 0), new Vector2(10, 5));

        // Act
        var result = Geometry.HasIntersection(rect1, rect2);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void HasIntersection_DisjointRectangles_ReturnsFalse()
    {
        // Arrange
        var rect1 = new Rect(new Vector2(0, 0), new Vector2(5, 5));
        var rect2 = new Rect(new Vector2(6, 6), new Vector2(10, 10));

        // Act
        var result = Geometry.HasIntersection(rect1, rect2);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void HasIntersection_OneRectangleInsideAnother_ReturnsTrue()
    {
        // Arrange
        var rect1 = new Rect(new Vector2(0, 0), new Vector2(10, 10));
        var rect2 = new Rect(new Vector2(2, 2), new Vector2(8, 8));

        // Act
        var result = Geometry.HasIntersection(rect1, rect2);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void HasIntersection_TouchingCorners_ReturnsTrue()
    {
        // Arrange
        var rect1 = new Rect(new Vector2(0, 0), new Vector2(5, 5));
        var rect2 = new Rect(new Vector2(5, 5), new Vector2(10, 10));

        // Act
        var result = Geometry.HasIntersection(rect1, rect2);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void HasIntersection_HorizontalOverlap_ReturnsTrue()
    {
        // Arrange
        var rect1 = new Rect(new Vector2(0, 0), new Vector2(5, 5));
        var rect2 = new Rect(new Vector2(3, 6), new Vector2(8, 10));

        // Act
        var result = Geometry.HasIntersection(rect1, rect2);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void HasIntersection_VerticalOverlap_ReturnsTrue()
    {
        // Arrange
        var rect1 = new Rect(new Vector2(0, 0), new Vector2(5, 5));
        var rect2 = new Rect(new Vector2(6, 3), new Vector2(10, 8));

        // Act
        var result = Geometry.HasIntersection(rect1, rect2);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void HasIntersection_IdenticalRectangles_ReturnsTrue()
    {
        // Arrange
        var rect1 = new Rect(new Vector2(0, 0), new Vector2(5, 5));
        var rect2 = new Rect(new Vector2(0, 0), new Vector2(5, 5));

        // Act
        var result = Geometry.HasIntersection(rect1, rect2);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void HasIntersection_EmptyRectangle_ReturnsFalse()
    {
        // Arrange
        var rect1 = new Rect(new Vector2(0, 0), new Vector2(0, 0));
        var rect2 = new Rect(new Vector2(1, 1), new Vector2(5, 5));

        // Act
        var result = Geometry.HasIntersection(rect1, rect2);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void HasIntersection_NegativeCoordinates_WorksCorrectly()
    {
        // Arrange
        var rect1 = new Rect(new Vector2(-5, -5), new Vector2(0, 0));
        var rect2 = new Rect(new Vector2(-3, -3), new Vector2(2, 2));

        // Act
        var result = Geometry.HasIntersection(rect1, rect2);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void HasIntersection_NoIntersectionWithNegativeCoordinates_ReturnsFalse()
    {
        // Arrange
        var rect1 = new Rect(new Vector2(-10, -10), new Vector2(-5, -5));
        var rect2 = new Rect(new Vector2(-4, -4), new Vector2(0, 0));

        // Act
        var result = Geometry.HasIntersection(rect1, rect2);

        // Assert
        Assert.False(result);
    }
}