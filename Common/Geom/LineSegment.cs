using ProtoBuf;
using Tyr.Common.Math;

namespace Tyr.Common.Geom;

[ProtoContract]
public struct LineSegment(Vector2 start, Vector2 end)
{
    [ProtoMember(1)] public Vector2 Start { get; init; } = start;
    [ProtoMember(2)] public Vector2 End { get; init; } = end;

    public readonly double Length() => Start.DistanceTo(End);

    public readonly double DistanceTo(Vector2 point)
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
}