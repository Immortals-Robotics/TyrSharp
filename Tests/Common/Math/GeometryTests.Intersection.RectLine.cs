using System.Numerics;
using Tyr.Common.Math;
using Tyr.Common.Math.Shapes;

namespace Tyr.Tests.Common.Math;

public partial class GeometryTests
{
    [Fact]
    public void Intersection_HorizontalLineThroughRectCenter_ReturnsTwoPoints()
    {
        var rect = new Rectangle(new Vector2(0, 0), new Vector2(4, 4));
        var line = Line.FromSlopeAndIntercept(0, 2); // y = 2

        var (point1, point2) = Geometry.Intersection(rect, line);

        Assert.NotNull(point1);
        Assert.NotNull(point2);

        // Expected intersection at left and right edges: (0,2) and (4,2)
        Assert.Equal(0f, point1.Value.X);
        Assert.Equal(2f, point1.Value.Y);
        Assert.Equal(4f, point2.Value.X);
        Assert.Equal(2f, point2.Value.Y);
    }

    [Fact]
    public void Intersection_VerticalLineThroughRectCenter_ReturnsTwoPoints()
    {
        var rect = new Rectangle(new Vector2(0, 0), new Vector2(4, 4));
        var line = Line.FromTwoPoints(new Vector2(2, -1), new Vector2(2, 5)); // x = 2

        var (point1, point2) = Geometry.Intersection(rect, line);

        Assert.NotNull(point1);
        Assert.NotNull(point2);

        // Expected intersection at top and bottom edges: (2,0) and (2,4)
        Assert.True(
            (Utils.ApproximatelyEqual(point1.Value, new Vector2(2, 0)) &&
             Utils.ApproximatelyEqual(point2.Value, new Vector2(2, 4))) ||
            (Utils.ApproximatelyEqual(point1.Value, new Vector2(2, 4)) &&
             Utils.ApproximatelyEqual(point2.Value, new Vector2(2, 0)))
        );
    }

    [Fact]
    public void Intersection_DiagonalLineThroughRectCenter_ReturnsTwoPoints()
    {
        var rect = new Rectangle(new Vector2(0, 0), new Vector2(4, 4));
        var line = Line.FromSlopeAndIntercept(1, 0); // y = x

        var (point1, point2) = Geometry.Intersection(rect, line);

        Assert.NotNull(point1);
        Assert.NotNull(point2);

        // Expected intersections at (0,0) and (4,4)
        Assert.True(
            (Utils.ApproximatelyEqual(point1.Value, new Vector2(0, 0)) &&
             Utils.ApproximatelyEqual(point2.Value, new Vector2(4, 4))) ||
            (Utils.ApproximatelyEqual(point1.Value, new Vector2(4, 4)) &&
             Utils.ApproximatelyEqual(point2.Value, new Vector2(0, 0)))
        );
    }

    [Fact]
    public void Intersection_DiagonalLineOppositeDirection_ReturnsTwoPoints()
    {
        var rect = new Rectangle(new Vector2(0, 0), new Vector2(4, 4));
        var line = Line.FromSlopeAndIntercept(-1, 4); // y = -x + 4

        var (point1, point2) = Geometry.Intersection(rect, line);

        Assert.NotNull(point1);
        Assert.NotNull(point2);

        // Expected intersections at (0,4) and (4,0)
        Assert.True(
            (Utils.ApproximatelyEqual(point1.Value, new Vector2(0, 4)) &&
             Utils.ApproximatelyEqual(point2.Value, new Vector2(4, 0))) ||
            (Utils.ApproximatelyEqual(point1.Value, new Vector2(4, 0)) &&
             Utils.ApproximatelyEqual(point2.Value, new Vector2(0, 4)))
        );
    }

    [Fact]
    public void Intersection_LinePassesThroughRectCorner_ReturnsTwoPoints()
    {
        var rect = new Rectangle(new Vector2(0, 0), new Vector2(4, 4));
        // Line passing through (0,0) and (2,2)
        var line = Line.FromTwoPoints(new Vector2(0, 0), new Vector2(2, 2));

        var (point1, point2) = Geometry.Intersection(rect, line);

        Assert.NotNull(point1);
        Assert.NotNull(point2);

        // Expected intersections at (0,0) and (4,4)
        Assert.True(
            (Utils.ApproximatelyEqual(point1.Value, new Vector2(0, 0)) &&
             Utils.ApproximatelyEqual(point2.Value, new Vector2(4, 4))) ||
            (Utils.ApproximatelyEqual(point1.Value, new Vector2(4, 4)) &&
             Utils.ApproximatelyEqual(point2.Value, new Vector2(0, 0)))
        );
    }

    [Fact]
    public void Intersection_LineDoesNotIntersectRect_ReturnsNull()
    {
        var rect = new Rectangle(new Vector2(0, 0), new Vector2(4, 4));
        var line = Line.FromSlopeAndIntercept(0, 5); // y = 5, above the rect

        var (point1, point2) = Geometry.Intersection(rect, line);

        Assert.Null(point1);
        Assert.Null(point2);
    }

    [Fact]
    public void Intersection_LineCoincidentWithRectEdge_ReturnsTwoPoints()
    {
        var rect = new Rectangle(new Vector2(0, 0), new Vector2(4, 4));
        var line = Line.FromTwoPoints(new Vector2(-1, 4), new Vector2(5, 4)); // y = 4, coincident with top edge

        var (point1, point2) = Geometry.Intersection(rect, line);

        Assert.NotNull(point1);
        Assert.NotNull(point2);

        // Expected intersections at (0,4) and (4,4)
        Assert.True(
            (Utils.ApproximatelyEqual(point1.Value, new Vector2(0, 4)) &&
             Utils.ApproximatelyEqual(point2.Value, new Vector2(4, 4))) ||
            (Utils.ApproximatelyEqual(point1.Value, new Vector2(4, 4)) &&
             Utils.ApproximatelyEqual(point2.Value, new Vector2(0, 4)))
        );
    }

    [Fact]
    public void Intersection_NonSquareRect_ReturnsTwoPoints()
    {
        var rect = new Rectangle(new Vector2(1, 1), new Vector2(5, 3)); // rectangle 4x2
        var line = Line.FromSlopeAndIntercept(0.5f, 0); // y = 0.5x

        var (point1, point2) = Geometry.Intersection(rect, line);

        Assert.NotNull(point1);
        Assert.NotNull(point2);

        // Should intersect at (2, 1) and (5, 2.5)
        Assert.True(
            (Utils.ApproximatelyEqual(point1.Value, new Vector2(2f, 1f)) &&
             Utils.ApproximatelyEqual(point2.Value, new Vector2(5f, 2.5f))) ||
            (Utils.ApproximatelyEqual(point1.Value, new Vector2(5f, 2.5f)) &&
             Utils.ApproximatelyEqual(point2.Value, new Vector2(2f, 1f)))
        );
    }

    [Fact]
    public void Intersection_OffsetRect_ReturnsTwoPoints()
    {
        var rect = new Rectangle(new Vector2(-2, -2), new Vector2(2, 2)); // centered at origin
        var line = Line.FromSlopeAndIntercept(0, 0); // y = 0

        var (point1, point2) = Geometry.Intersection(rect, line);

        Assert.NotNull(point1);
        Assert.NotNull(point2);

        // Expected intersections at (-2,0) and (2,0)
        Assert.True(
            (Utils.ApproximatelyEqual(point1.Value, new Vector2(-2, 0)) &&
             Utils.ApproximatelyEqual(point2.Value, new Vector2(2, 0))) ||
            (Utils.ApproximatelyEqual(point1.Value, new Vector2(2, 0)) &&
             Utils.ApproximatelyEqual(point2.Value, new Vector2(-2, 0)))
        );
    }
}