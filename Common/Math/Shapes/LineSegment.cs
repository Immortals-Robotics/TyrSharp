using System.Numerics;

namespace Tyr.Common.Math.Shapes;

public struct LineSegment(Vector2 start, Vector2 end)
{
    public Vector2 Start { get; set; } = start;
    public Vector2 End { get; set; } = end;

    public readonly float Length() => Vector2.Distance(Start, End);

    public readonly Vector2 ClosestPoint(Vector2 point)
    {
        var seg = End - Start;
        var toPoint = point - Start;

        var segDot = Vector2.Dot(seg, seg);
        if (segDot < 1e-6f) return Start; // Degenerate

        var t = Vector2.Dot(toPoint, seg) / segDot;

        if (t < 0f) return Start;
        else if (t > 1f) return End;
        else return Start + seg * t;
    }

    public float Distance(Vector2 point)
    {
        var seg = End - Start;
        var toPoint = point - Start;

        var segDot = Vector2.Dot(seg, seg);
        if (segDot < 1e-6f) return Vector2.Distance(point, Start); // Degenerate

        var t = Vector2.Distance(toPoint, seg) / segDot;

        if (t < 0f)
            return Vector2.Distance(point, Start);
        else if (t > 1f)
            return Vector2.Distance(point, End);
        else
            return Vector2.Distance(point, Start + seg * t);
    }
}