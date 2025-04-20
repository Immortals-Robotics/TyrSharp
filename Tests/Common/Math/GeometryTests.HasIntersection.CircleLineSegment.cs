using System.Numerics;
using Tyr.Common.Math;
using Tyr.Common.Math.Shapes;

namespace Tyr.Tests.Common.Math;

public partial class GeometryTests
{
    [Fact]
    public void HasIntersection_LineSegmentCrossesCircle_ReturnsTrue()
    {
        // Arrange
        var circle = new Circle { Center = new Vector2(5, 5), Radius = 3 };
        var segment = new LineSegment { Start = new Vector2(0, 5), End = new Vector2(10, 5) };

        // Act
        var result = Geometry.HasIntersection(circle, segment);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void HasIntersection_LineSegmentOutsideCircle_ReturnsFalse()
    {
        // Arrange
        var circle = new Circle { Center = new Vector2(5, 5), Radius = 2 };
        var segment = new LineSegment { Start = new Vector2(8, 8), End = new Vector2(12, 12) };

        // Act
        var result = Geometry.HasIntersection(circle, segment);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void HasIntersection_LineSegmentTangentToCircle_ReturnsTrue()
    {
        // Arrange
        var circle = new Circle { Center = new Vector2(5, 5), Radius = 3 };
        var segment = new LineSegment { Start = new Vector2(2, 8), End = new Vector2(8, 8) };

        // Act
        var result = Geometry.HasIntersection(circle, segment);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void HasIntersection_LineSegmentEntirelyInsideCircle_ReturnsTrue()
    {
        // Arrange
        var circle = new Circle { Center = new Vector2(5, 5), Radius = 5 };
        var segment = new LineSegment { Start = new Vector2(4, 4), End = new Vector2(6, 6) };

        // Act
        var result = Geometry.HasIntersection(circle, segment);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void HasIntersection_OneEndpointInsideCircle_ReturnsTrue()
    {
        // Arrange
        var circle = new Circle { Center = new Vector2(5, 5), Radius = 3 };
        var segment = new LineSegment { Start = new Vector2(4, 4), End = new Vector2(10, 10) };

        // Act
        var result = Geometry.HasIntersection(circle, segment);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void HasIntersection_BothEndpointsInsideCircle_ReturnsTrue()
    {
        // Arrange
        var circle = new Circle { Center = new Vector2(5, 5), Radius = 5 };
        var segment = new LineSegment { Start = new Vector2(3, 3), End = new Vector2(7, 7) };

        // Act
        var result = Geometry.HasIntersection(circle, segment);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void HasIntersection_LineWouldIntersectButSegmentDoesNot_ReturnsFalse()
    {
        // Arrange
        var circle = new Circle { Center = new Vector2(5, 5), Radius = 2 };
        var segment = new LineSegment { Start = new Vector2(0, 0), End = new Vector2(2, 2) };
        // This segment is on a line that would eventually intersect the circle,
        // but the segment itself doesn't extend far enough

        // Act
        var result = Geometry.HasIntersection(circle, segment);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void HasIntersection_TouchingEndpoint_ReturnsTrue()
    {
        // Arrange
        var circle = new Circle { Center = new Vector2(5, 5), Radius = 5 };
        var segment = new LineSegment { Start = new Vector2(10, 5), End = new Vector2(15, 5) };
        // The endpoint is exactly on the circle boundary

        // Act
        var result = Geometry.HasIntersection(circle, segment);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void HasIntersection_SegmentAlongDiameter_ReturnsTrue()
    {
        // Arrange
        var circle = new Circle { Center = new Vector2(5, 5), Radius = 5 };
        var segment = new LineSegment { Start = new Vector2(0, 5), End = new Vector2(10, 5) };
        // Segment goes along diameter of circle

        // Act
        var result = Geometry.HasIntersection(circle, segment);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void HasIntersection_ZeroLengthSegmentInsideCircle_ReturnsTrue()
    {
        // Arrange
        var circle = new Circle { Center = new Vector2(5, 5), Radius = 3 };
        var segment = new LineSegment { Start = new Vector2(5, 5), End = new Vector2(5, 5) };
        // Point segment at center

        // Act
        var result = Geometry.HasIntersection(circle, segment);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void HasIntersection_ZeroLengthSegmentOutsideCircle_ReturnsFalse()
    {
        // Arrange
        var circle = new Circle { Center = new Vector2(5, 5), Radius = 3 };
        var segment = new LineSegment { Start = new Vector2(10, 10), End = new Vector2(10, 10) };
        // Point segment outside

        // Act
        var result = Geometry.HasIntersection(circle, segment);

        // Assert
        Assert.False(result);
    }
}