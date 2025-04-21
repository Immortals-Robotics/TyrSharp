using System.Numerics;

namespace Tyr.Common.Math.Shapes;

public readonly record struct Rect
{
    public Vector2 Min { get; }

    public Vector2 Max { get; }

    public Rect(Vector2 p1, Vector2 p2)
    {
        Min = Vector2.Min(p1, p2);
        Max = Vector2.Max(p1, p2);
    }

    public static Rect FromCornerAndSize(Vector2 corner, Vector2 size) => new(corner, corner + size);

    public static Rect FromCornerAndSize(Vector2 corner, float width, float height) =>
        FromCornerAndSize(corner, new Vector2(width, height));

    public static Rect FromCenterAndSize(Vector2 center, Vector2 size) => new(center - size / 2f, center + size / 2f);

    public static Rect FromCenterAndSize(Vector2 center, float width, float height) =>
        FromCenterAndSize(center, new Vector2(width, height));

    public float Circumference => (Width + Height) * 2f;
    public float Area => Width * Height;

    public Vector2 Size => Max - Min;
    public float Width => Max.X - Min.X;
    public float Height => Max.Y - Min.Y;
    public Vector2 Center => (Min + Max) / 2f;

    public float Distance(Vector2 point)
    {
        var dx = MathF.Max(Min.X - point.X, point.X - Max.X);
        var dy = MathF.Max(Min.Y - point.Y, point.Y - Max.Y);
        return MathF.Max(dx, dy);
    }

    public bool Inside(Vector2 point, float margin = 0f) => Distance(point) <= margin;

    public Vector2 NearestOutside(Vector2 point, float margin = 0)
    {
        var dxMin = MathF.Abs(point.X - Min.X);
        var dxMax = MathF.Abs(point.X - Max.X);
        var dyMin = MathF.Abs(point.Y - Min.Y);
        var dyMax = MathF.Abs(point.Y - Max.Y);

        var minDist = System.Math.Min(
            System.Math.Min(dxMin, dxMax),
            System.Math.Min(dyMin, dyMax));

        if (Utils.ApproximatelyEqual(minDist, dxMin))
            return point with { X = Min.X - margin };
        if (Utils.ApproximatelyEqual(minDist, dxMax))
            return point with { X = Max.X + margin };
        if (Utils.ApproximatelyEqual(minDist, dyMin))
            return point with { Y = Min.Y - margin };
        else
            return point with { Y = Max.Y + margin };
    }

    public (LineSegment, LineSegment, LineSegment, LineSegment) Edges()
    {
        return (
            new LineSegment { Start = Min, End = new Vector2(Min.X, Max.Y) },
            new LineSegment { Start = Min, End = new Vector2(Max.X, Min.Y) },
            new LineSegment { Start = Max, End = new Vector2(Min.X, Max.Y) },
            new LineSegment { Start = Max, End = new Vector2(Max.X, Min.Y) });
    }
}