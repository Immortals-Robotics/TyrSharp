using ProtoBuf;
using Tyr.Common.Math;
using System.Numerics;

namespace Tyr.Common.Shape;

[ProtoContract]
public struct Rect(Vector2 p1, Vector2 p2) : IShape
{
    [ProtoMember(1)]
    public Vector2 Min { get; set; } = new(
        MathF.Min(p1.X, p2.X),
        MathF.Min(p1.Y, p2.Y));

    [ProtoMember(2)]
    public Vector2 Max { get; set; } = new(
        MathF.Max(p1.X, p2.X),
        MathF.Max(p1.Y, p2.Y)
    );

    public Rect(Vector2 position, float width, float height)
        : this(position, position + new Vector2(width, height))
    {
    }

    public readonly bool Inside(Vector2 point, float margin = 0f) => Distance(point) <= margin;

    public Vector2 NearestOutside(Vector2 point, float margin = 0)
    {
        float dxMin = MathF.Abs(point.X - Min.X);
        float dxMax = MathF.Abs(point.X - Max.X);
        float dyMin = MathF.Abs(point.Y - Min.Y);
        float dyMax = MathF.Abs(point.Y - Max.Y);

        float minDist = new[] { dxMin, dxMax, dyMin, dyMax }.Min();

        if (Utils.ApproximatelyEqual(minDist, dxMin))
            return new Vector2(Min.X - margin, point.Y);
        if (Utils.ApproximatelyEqual(minDist, dxMax))
            return new Vector2(Max.X + margin, point.Y);
        if (Utils.ApproximatelyEqual(minDist, dyMin))
            return new Vector2(point.X, Min.Y - margin);

        return new Vector2(point.X, Max.Y + margin);
    }

    public float Circumference => (Width + Height) * 2f;
    public float Area => Width * Height;

    public readonly float Distance(Vector2 point)
    {
        float dx = MathF.Max(Min.X - point.X, point.X - Max.X);
        float dy = MathF.Max(Min.Y - point.Y, point.Y - Max.Y);
        return MathF.Max(dx, dy);
    }

    public readonly bool Intersects(Rect other)
    {
        return Min.X <= other.Max.X && Max.X >= other.Min.X &&
               Min.Y <= other.Max.Y && Max.Y >= other.Min.Y;
    }

    public readonly IEnumerable<Vector2> Intersection(Line line)
    {
        var segments = new[]
        {
            new LineSegment(Min, new Vector2(Min.X, Max.Y)),
            new LineSegment(Min, new Vector2(Max.X, Min.Y)),
            new LineSegment(Max, new Vector2(Min.X, Max.Y)),
            new LineSegment(Max, new Vector2(Max.X, Min.Y))
        };

        foreach (var seg in segments)
        {
            var pt = line.Intersect(seg);
            if (pt.HasValue)
                yield return pt.Value;
        }
    }

    public readonly bool IsCircleCross(Vector2 point)
    {
        return Min.X <= point.X && Max.X >= point.X &&
               Min.Y <= point.Y && Max.Y >= point.Y;
    }

    public readonly Vector2 Size => Max - Min;
    public readonly float Width => Max.X - Min.X;
    public readonly float Height => Max.Y - Min.Y;
    public readonly Vector2 Center => (Min + Max) / 2f;
}