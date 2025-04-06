using ProtoBuf;
using Tyr.Common.Math;

namespace Tyr.Common.Shape;

[ProtoContract]
public struct LineSegment(Vector2 start, Vector2 end) : IShape
{
    [ProtoMember(1)] public Vector2 Start { get; init; } = start;
    [ProtoMember(2)] public Vector2 End { get; init; } = end;

    public readonly float Length() => Start.DistanceTo(End);

    public readonly Vector2 ClosestPoint(Vector2 point)
    {
        Vector2 seg = End - Start;
        Vector2 toPoint = point - Start;

        float segDot = seg.Dot(seg);
        if (segDot < 1e-6f) return Start; // Degenerate

        float t = toPoint.Dot(seg) / segDot;

        if (t < 0f) return Start;
        else if (t > 1f) return End;
        else return Start + seg * t;
    }

    public float Circumference => 2f * Length();
    public float Area => 0f;

    public float Distance(Vector2 point)
    {
        Vector2 seg = End - Start;
        Vector2 toPoint = point - Start;

        float segDot = seg.Dot(seg);
        if (segDot < 1e-6f) return point.DistanceTo(Start); // Degenerate

        float t = toPoint.DistanceTo(seg) / segDot;

        if (t < 0f)
            return point.DistanceTo(Start);
        else if (t > 1f)
            return point.DistanceTo(End);
        else
            return point.DistanceTo(Start + seg * t);
    }

    public bool Inside(Vector2 point, float margin = 0)
    {
        return false;
    }

    public Vector2 NearestOutside(Vector2 point, float margin = 0)
    {
        return point;
    }
}