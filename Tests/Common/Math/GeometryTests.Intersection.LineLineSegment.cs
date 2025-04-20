using System.Numerics;
using Tyr.Common.Math;
using Tyr.Common.Math.Shapes;

namespace Tyr.Tests.Common.Math;

public partial class GeometryTests
{
    [Fact]
    public void Intersection_IntersectsWithinSegment_ReturnsIntersectionPoint()
    {
        var line = Line.FromSlopeAndIntercept(1, 0); // y = x
        var segment = new LineSegment { Start = new Vector2(0, 2), End = new Vector2(2, 0) };

        var intersection = Geometry.Intersection(line, segment);

        Assert.NotNull(intersection);
        Assert.Equal(new Vector2(1f, 1f), intersection.Value, Utils.ApproximatelyEqual);
    }

    [Fact]
    public void Intersection_IntersectsOutsideSegment_ReturnsNull()
    {
        var line = Line.FromSlopeAndIntercept(1, 0); // y = x
        var segment = new LineSegment { Start = new Vector2(5, 3), End = new Vector2(7, 1) };

        var intersection = Geometry.Intersection(line, segment);

        Assert.Null(intersection);
    }

    [Fact]
    public void Intersection_ParallelNoIntersection_ReturnsNull()
    {
        var line = Line.FromSlopeAndIntercept(1, 0); // y = x
        var segment = new LineSegment { Start = new Vector2(0, 1), End = new Vector2(1, 2) }; // y = x + 1

        var intersection = Geometry.Intersection(line, segment);

        Assert.Null(intersection);
    }

    [Fact]
    public void Intersection_VerticalLineHorizontalSegment_ReturnsIntersectionPoint()
    {
        var line = Line.FromTwoPoints(new Vector2(2, 0), new Vector2(2, 5)); // x = 2
        var segment = new LineSegment { Start = new Vector2(0, 3), End = new Vector2(4, 3) };

        var intersection = Geometry.Intersection(line, segment);

        Assert.NotNull(intersection);
        Assert.Equal(new Vector2(2f, 3f), intersection.Value, Utils.ApproximatelyEqual);
    }

    [Fact]
    public void Intersection_HorizontalLineVerticalSegment_ReturnsIntersectionPoint()
    {
        var line = Line.FromSlopeAndIntercept(0, 3); // y = 3
        var segment = new LineSegment { Start = new Vector2(2, 0), End = new Vector2(2, 5) };

        var intersection = Geometry.Intersection(line, segment);

        Assert.NotNull(intersection);
        Assert.Equal(new Vector2(2f, 3f), intersection.Value, Utils.ApproximatelyEqual);
    }

    [Fact]
    public void Intersection_TouchesSegmentEndpoint_ReturnsIntersectionPoint()
    {
        var line = Line.FromSlopeAndIntercept(1, 0); // y = x
        var segment = new LineSegment { Start = new Vector2(2, 2), End = new Vector2(4, 0) };

        var intersection = Geometry.Intersection(line, segment);

        Assert.NotNull(intersection);
        Assert.Equal(new Vector2(2f, 2f), intersection.Value, Utils.ApproximatelyEqual);
    }

    [Fact]
    public void Intersection_DiagonalLineAndSegment_ReturnsIntersectionPoint()
    {
        var line = Line.FromSlopeAndIntercept(2, -2); // y = 2x - 2
        var segment = new LineSegment { Start = new Vector2(0, 0), End = new Vector2(4, 0) };

        var intersection = Geometry.Intersection(line, segment);

        Assert.NotNull(intersection);
        Assert.Equal(new Vector2(1f, 0f), intersection.Value, Utils.ApproximatelyEqual);
    }

    [Fact]
    public void Intersection_LineOverlapsSegment_ReturnsNull()
    {
        var line = Line.FromSlopeAndIntercept(0, 2); // y = 2
        var segment = new LineSegment { Start = new Vector2(0, 2), End = new Vector2(4, 2) };

        var intersection = Geometry.Intersection(line, segment);

        Assert.Null(intersection);
    }

    [Fact]
    public void Intersection_SegmentTouchesLineAtEndpoint_ReturnsIntersectionPoint()
    {
        var line = Line.FromSlopeAndIntercept(0, 2); // y = 2
        var segment = new LineSegment { Start = new Vector2(1, 0), End = new Vector2(1, 2) };

        var intersection = Geometry.Intersection(line, segment);

        Assert.NotNull(intersection);
        Assert.Equal(new Vector2(1f, 2f), intersection.Value, Utils.ApproximatelyEqual);
    }
}