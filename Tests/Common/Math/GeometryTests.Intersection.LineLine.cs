using System.Numerics;
using Tyr.Common.Math;
using Tyr.Common.Math.Shapes;

namespace Tyr.Tests.Common.Math;

public partial class GeometryTests
{
    [Fact]
    public void Intersection_ParallelLines_ReturnsNull()
    {
        var line1 = Line.FromSlopeAndIntercept(2, 0);
        var line2 = Line.FromSlopeAndIntercept(2, 5);

        var intersection = Geometry.Intersection(line1, line2);

        Assert.Null(intersection);
    }

    [Fact]
    public void Intersection_PerpendicularLines_ReturnsIntersectionPoint()
    {
        var line1 = Line.FromSlopeAndIntercept(0, 2); // y = 2
        var line2 = Line.FromTwoPoints(new Vector2(3, 0), new Vector2(3, 5)); // x = 3

        var intersection = Geometry.Intersection(line1, line2);

        Assert.NotNull(intersection);
        Assert.Equal(3f, intersection!.Value.X);
        Assert.Equal(2f, intersection.Value.Y);
    }

    [Fact]
    public void Intersection_FirstLineHorizontal_ReturnsIntersectionPoint()
    {
        var horizontalLine = Line.FromSlopeAndIntercept(0, 4); // y = 4
        var diagonalLine = Line.FromSlopeAndIntercept(2, 0); // y = 2x

        var intersection = Geometry.Intersection(horizontalLine, diagonalLine);

        Assert.NotNull(intersection);
        Assert.Equal(2f, intersection!.Value.X);
        Assert.Equal(4f, intersection.Value.Y);
    }

    [Fact]
    public void Intersection_SecondLineHorizontal_ReturnsIntersectionPoint()
    {
        var diagonalLine = Line.FromSlopeAndIntercept(2, 0); // y = 2x
        var horizontalLine = Line.FromSlopeAndIntercept(0, 4); // y = 4

        var intersection = Geometry.Intersection(diagonalLine, horizontalLine);

        Assert.NotNull(intersection);
        Assert.Equal(2f, intersection!.Value.X);
        Assert.Equal(4f, intersection.Value.Y);
    }

    [Fact]
    public void Intersection_FirstLineVertical_ReturnsIntersectionPoint()
    {
        var verticalLine = Line.FromTwoPoints(new Vector2(3, 0), new Vector2(3, 5)); // x = 3
        var diagonalLine = Line.FromSlopeAndIntercept(2, 0); // y = 2x

        var intersection = Geometry.Intersection(verticalLine, diagonalLine);

        Assert.NotNull(intersection);
        Assert.Equal(3f, intersection!.Value.X);
        Assert.Equal(6f, intersection.Value.Y);
    }

    [Fact]
    public void Intersection_SecondLineVertical_ReturnsIntersectionPoint()
    {
        var diagonalLine = Line.FromSlopeAndIntercept(2, 0); // y = 2x
        var verticalLine = Line.FromTwoPoints(new Vector2(3, 0), new Vector2(3, 5)); // x = 3

        var intersection = Geometry.Intersection(diagonalLine, verticalLine);

        Assert.NotNull(intersection);
        Assert.Equal(3f, intersection!.Value.X);
        Assert.Equal(6f, intersection.Value.Y);
    }

    [Fact]
    public void Intersection_DiagonalLines_ReturnsIntersectionPoint()
    {
        var line1 = Line.FromSlopeAndIntercept(2, 1); // y = 2x + 1
        var line2 = Line.FromSlopeAndIntercept(-1, 4); // y = -x + 4

        var intersection = Geometry.Intersection(line1, line2);

        Assert.NotNull(intersection);
        Assert.Equal(1f, intersection!.Value.X);
        Assert.Equal(3f, intersection.Value.Y);
    }

    [Fact]
    public void Intersection_VerticalParallelLines_ReturnsNull()
    {
        var line1 = Line.FromTwoPoints(new Vector2(3, 0), new Vector2(3, 5)); // x = 3
        var line2 = Line.FromTwoPoints(new Vector2(5, 0), new Vector2(5, 5)); // x = 5

        var intersection = Geometry.Intersection(line1, line2);

        Assert.Null(intersection);
    }

    [Fact]
    public void Intersection_HorizontalParallelLines_ReturnsNull()
    {
        var line1 = Line.FromSlopeAndIntercept(0, 2); // y = 2
        var line2 = Line.FromSlopeAndIntercept(0, 4); // y = 4

        var intersection = Geometry.Intersection(line1, line2);

        Assert.Null(intersection);
    }
}