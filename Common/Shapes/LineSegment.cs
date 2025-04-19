using System.Numerics;
using ProtoBuf;

namespace Tyr.Common.Shapes;

[ProtoContract]
public struct LineSegment(Vector2 start, Vector2 end) : IShape
{
    [ProtoMember(1)] public Vector2 Start { get; init; } = start;
    [ProtoMember(2)] public Vector2 End { get; init; } = end;

    public readonly float Length() => Vector2.Distance(Start, End);

    public readonly Vector2 ClosestPoint(Vector2 point)
    {
        Vector2 seg = End - Start;
        Vector2 toPoint = point - Start;

        float segDot = Vector2.Dot(seg, seg);
        if (segDot < 1e-6f) return Start; // Degenerate

        float t = Vector2.Dot(toPoint, seg) / segDot;

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

        float segDot = Vector2.Dot(seg, seg);
        if (segDot < 1e-6f) return Vector2.Distance(point, Start); // Degenerate

        float t = Vector2.Distance(toPoint, seg) / segDot;

        if (t < 0f)
            return Vector2.Distance(point, Start);
        else if (t > 1f)
            return Vector2.Distance(point, End);
        else
            return Vector2.Distance(point, Start + seg * t);
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