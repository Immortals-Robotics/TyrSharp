using System.Numerics;
using Tyr.Common.Math;
using Tyr.Common.Math.Shapes;

namespace Tyr.Tests.Common.Math;

public partial class GeometryTests
{
    [Fact]
    public void Intersection_HorizontalLineCrossesCircle_ReturnsTwoPoints()
    {
        // Horizontal line y = 0 crossing a circle at origin with radius 1
        var line = Line.FromSlopeAndIntercept(0, 0); // y = 0
        var circle = new Circle { Center = Vector2.Zero, Radius = 1f };

        var (point1, point2) = Geometry.Intersection(line, circle);

        Assert.NotNull(point1);
        Assert.NotNull(point2);

        // Expected intersection points at (-1,0) and (1,0)
        if (point1.Value.X > point2.Value.X)
            (point1, point2) = (point2, point1);

        Assert.Equal(new Vector2(-1f, 0f), point1.Value, Utils.ApproximatelyEqual);
        Assert.Equal(new Vector2(1f, 0f), point2.Value, Utils.ApproximatelyEqual);
    }

    [Fact]
    public void Intersection_VerticalLineCrossesCircle_ReturnsTwoPoints()
    {
        // Vertical line x = 0 crossing a circle at origin with radius 1
        var line = Line.FromTwoPoints(new Vector2(0, -5), new Vector2(0, 5)); // x = 0
        var circle = new Circle { Center = Vector2.Zero, Radius = 1f };

        var (point1, point2) = Geometry.Intersection(line, circle);

        Assert.NotNull(point1);
        Assert.NotNull(point2);

        // Expected intersection points at (0,-1) and (0,1)
        if (point1.Value.Y > point2.Value.Y)
            (point1, point2) = (point2, point1);

        Assert.Equal(new Vector2(0f, -1f), point1.Value, Utils.ApproximatelyEqual);
        Assert.Equal(new Vector2(0f, 1f), point2.Value, Utils.ApproximatelyEqual);
    }

    [Fact]
    public void Intersection_LineTangentToCircle_ReturnsSinglePoint()
    {
        // Line y = 1 tangent to circle at (0,0) with radius 1
        var line = Line.FromSlopeAndIntercept(0, 1); // y = 1
        var circle = new Circle { Center = Vector2.Zero, Radius = 1f };

        var (point1, point2) = Geometry.Intersection(line, circle);

        Assert.NotNull(point1);
        Assert.Null(point2);

        // Expected tangent point at (0,1)
        Assert.Equal(new Vector2(0f, 1f), point1.Value, Utils.ApproximatelyEqual);
    }

    [Fact]
    public void Intersection_DiagonalLineCrossesCircle_ReturnsTwoPoints()
    {
        // Line y = x crossing a circle at origin with radius 1
        var line = Line.FromSlopeAndIntercept(1, 0); // y = x
        var circle = new Circle { Center = Vector2.Zero, Radius = 1f };

        var (point1, point2) = Geometry.Intersection(line, circle);

        Assert.NotNull(point1);
        Assert.NotNull(point2);

        // Expected intersection points at approximately (0.7071, 0.7071) and (-0.7071, -0.7071)
        // The exact value is 1/√2 ≈ 0.7071...
        var expectedCoord = 1f / MathF.Sqrt(2f);

        if (point1.Value.X > point2.Value.X)
            (point1, point2) = (point2, point1);

        Assert.Equal(new Vector2(-expectedCoord, -expectedCoord), point1.Value, Utils.ApproximatelyEqual);
        Assert.Equal(new Vector2(expectedCoord, expectedCoord), point2.Value, Utils.ApproximatelyEqual);
    }

    [Fact]
    public void Intersection_LineDoesNotIntersectCircle_ReturnsNull()
    {
        // Line y = 2 not touching circle at origin with radius 1
        var line = Line.FromSlopeAndIntercept(0, 2); // y = 2
        var circle = new Circle { Center = Vector2.Zero, Radius = 1f };

        var (point1, point2) = Geometry.Intersection(line, circle);

        Assert.Null(point1);
        Assert.Null(point2);
    }

    [Fact]
    public void Intersection_LinePassesThroughCircleCenter_ReturnsTwoPoints()
    {
        // Line passing through center of circle at (3,2) with radius 2
        var line = Line.FromTwoPoints(new Vector2(3, 2), new Vector2(5, 2));
        var circle = new Circle { Center = new Vector2(3, 2), Radius = 2f };

        var (point1, point2) = Geometry.Intersection(line, circle);

        Assert.NotNull(point1);
        Assert.NotNull(point2);

        // Expected points at (1,2) and (5,2)
        if (point1.Value.X > point2.Value.X)
            (point1, point2) = (point2, point1);

        Assert.Equal(new Vector2(1f, 2f), point1.Value, Utils.ApproximatelyEqual);
        Assert.Equal(new Vector2(5f, 2f), point2.Value, Utils.ApproximatelyEqual);
    }

    [Fact]
    public void Intersection_NonZeroCircleCenterDiagonalLine_ReturnsTwoPoints()
    {
        // Circle at (3,4) with radius 2, line y = x + 1
        var line = Line.FromSlopeAndIntercept(1, 1); // y = x + 1
        var circle = new Circle { Center = new Vector2(3, 4), Radius = 2f };

        var (point1, point2) = Geometry.Intersection(line, circle);

        Assert.NotNull(point1);
        Assert.NotNull(point2);

        // Check both points are on the line
        Assert.Equal(point1.Value.Y, point1.Value.X + 1);
        Assert.Equal(point2.Value.Y, point2.Value.X + 1);

        // Check both points are on the circle perimeter
        Assert.Equal(Vector2.Distance(point1.Value, circle.Center), circle.Radius);
        Assert.Equal(Vector2.Distance(point2.Value, circle.Center), circle.Radius);
    }
}