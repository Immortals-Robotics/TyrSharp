using System.Numerics;

namespace Tyr.Common.Math.Shapes;

public readonly record struct LineSegment
{
    public Vector2 Start { get; init; }
    public Vector2 End { get; init; }

    public readonly float Length() => Vector2.Distance(Start, End);

    public readonly Vector2 ClosestPoint(Vector2 point)
    {
        var seg = End - Start;
        var toPoint = point - Start;

        var segDot = Vector2.Dot(seg, seg);
        if (Utils.ApproximatelyZero(segDot)) return Start; // Degenerate (very short segment)

        var t = Vector2.Dot(toPoint, seg) / segDot;
        t = System.Math.Clamp(t, 0f, 1f);

        return Start + seg * t;
    }

    public float Distance(Vector2 point) => Vector2.Distance(ClosestPoint(point), point);
}