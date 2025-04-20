using System.Numerics;

namespace Tyr.Common.Math.Shapes;

public struct Triangle
{
    public Vector2 Corner1;
    public Vector2 Corner2;
    public Vector2 Corner3;

    public Triangle(Vector2 corner1, Vector2 corner2, Vector2 corner3)
    {
        Corner1 = corner1;
        Corner2 = corner2;
        Corner3 = corner3;

        // Sort corners clockwise around Corner1
        if (Area < 0)
            (Corner2, Corner3) = (Corner3, Corner2);

        Assert.IsPositive(Area);
    }

    public float Circumference
    {
        get
        {
            var a = Vector2.Distance(Corner1, Corner2);
            var b = Vector2.Distance(Corner2, Corner3);
            var c = Vector2.Distance(Corner3, Corner1);
            return a + b + c;
        }
    }

    public float Area => (Corner2 - Corner1).Cross(Corner3 - Corner1) * 0.5f;

    public float Distance(Vector2 point)
    {
        throw new NotImplementedException();
    }

    public bool Inside(Vector2 point, float margin = 0) => Distance(point) < margin;

    public Vector2 NearestOutside(Vector2 point, float margin = 0)
    {
        throw new NotImplementedException();
    }
}