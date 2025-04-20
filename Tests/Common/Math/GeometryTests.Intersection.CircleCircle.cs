using System.Numerics;
using Tyr.Common.Math;
using Tyr.Common.Math.Shapes;

namespace Tyr.Tests.Common.Math;

public partial class GeometryTests
{
    [Fact]
    public void CircleIntersection_NoIntersection_WhenTooFarApart()
    {
        var circle1 = new Circle { Center = Vector2.Zero, Radius = 1f };
        var circle2 = new Circle { Center = new Vector2(3f, 0f), Radius = 1f };

        var (point1, point2) = Geometry.Intersection(circle1, circle2);

        Assert.Null(point1);
        Assert.Null(point2);
    }

    [Fact]
    public void CircleIntersection_NoIntersection_WhenOneInsideOther()
    {
        var circle1 = new Circle { Center = Vector2.Zero, Radius = 2f };
        var circle2 = new Circle { Center = new Vector2(0.5f, 0f), Radius = 1f };

        var (point1, point2) = Geometry.Intersection(circle1, circle2);

        Assert.Null(point1);
        Assert.Null(point2);
    }

    [Fact]
    public void CircleIntersection_SinglePoint_WhenTangent()
    {
        var circle1 = new Circle { Center = Vector2.Zero, Radius = 1f };
        var circle2 = new Circle { Center = new Vector2(2f, 0f), Radius = 1f };

        var (point1, point2) = Geometry.Intersection(circle1, circle2);

        Assert.NotNull(point1);
        Assert.Null(point2);
        Assert.Equal(new Vector2(1f, 0f), point1!.Value);
    }

    [Fact]
    public void CircleIntersection_TwoPoints_WhenOverlapping()
    {
        var circle1 = new Circle { Center = Vector2.Zero, Radius = 2f };
        var circle2 = new Circle { Center = new Vector2(2f, 0f), Radius = 2f };

        var (point1, point2) = Geometry.Intersection(circle1, circle2);

        Assert.NotNull(point1);
        Assert.NotNull(point2);

        // Expected intersection points at (1, ±√3)
        var expectedY = MathF.Sqrt(3f);
        Assert.Equal(1f, point1!.Value.X);
        Assert.Equal(expectedY, MathF.Abs(point1.Value.Y), 0.0001f);
        Assert.Equal(1f, point2!.Value.X);
        Assert.Equal(-expectedY, point2.Value.Y, 0.0001f);
    }

    [Fact]
    public void CircleIntersection_TwoPoints_WhenIntersectingAtAngle()
    {
        var circle1 = new Circle { Center = Vector2.Zero, Radius = 1f };
        var circle2 = new Circle { Center = new Vector2(1f, 1f), Radius = 1f };

        var (point1, point2) = Geometry.Intersection(circle1, circle2);

        Assert.NotNull(point1);
        Assert.NotNull(point2);

        // Verify points are equidistant from both circle centers
        Assert.Equal(1f, Vector2.Distance(point1!.Value, circle1.Center), 0.0001f);
        Assert.Equal(1f, Vector2.Distance(point1.Value, circle2.Center), 0.0001f);
        Assert.Equal(1f, Vector2.Distance(point2!.Value, circle1.Center), 0.0001f);
        Assert.Equal(1f, Vector2.Distance(point2.Value, circle2.Center), 0.0001f);
    }

    [Fact]
    public void CircleIntersection_SameCircle_ReturnsNull()
    {
        var circle = new Circle { Center = Vector2.Zero, Radius = 1f };

        var (point1, point2) = Geometry.Intersection(circle, circle);

        Assert.Null(point1);
        Assert.Null(point2);
    }
}