using ProtoBuf;
using Tyr.Common.Math;

namespace Tyr.Common.Geom;

[ProtoContract]
public struct Robot(Vector2 center, float radius, Angle angle)
{
    [ProtoMember(1)] public Vector2 Center { get; } = center;
    [ProtoMember(2)] public float Radius { get; } = radius;
    [ProtoMember(3)] public Angle Angle { get; } = angle;

    private static readonly Angle HalfArcAngle = Angle.FromDeg(50f);
    private const float KickerDepth = 150f;

    public readonly float FrontDistance() => Radius * HalfArcAngle.Cos();

    public readonly bool Inside(Vector2 point)
    {
        var rel = point - Center;
        var dis = rel.Length();

        var start = Angle - HalfArcAngle;
        var end = Angle + HalfArcAngle;

        return dis < FrontDistance() || (dis <= Radius && rel.ToAngle().IsBetween(start, end));
    }

    public readonly bool CanKick(Vector2 point, float kickerDepth = KickerDepth)
    {
        var a = Angle;
        var ah = HalfArcAngle;
        var r = Radius;

        Vector2 p1 = Center + (a + ah).ToUnitVec() * r - a.ToUnitVec() * kickerDepth * 0.5f;
        Vector2 p2 = Center + (a - ah).ToUnitVec() * r - a.ToUnitVec() * kickerDepth * 0.5f;
        Vector2 p3 = Center + (a - ah).ToUnitVec() * r + a.ToUnitVec() * kickerDepth;
        Vector2 p4 = Center + (a + ah).ToUnitVec() * r + a.ToUnitVec() * kickerDepth;

        Vector2 v1 = point - p1;
        Vector2 v2 = point - p2;
        Vector2 v3 = point - p3;
        Vector2 v4 = point - p4;

        float cross1 = Cross(p2 - p1, v1);
        float cross2 = Cross(p3 - p2, v2);
        float cross3 = Cross(p4 - p3, v3);
        float cross4 = Cross(p1 - p4, v4);

        int s1 = Utils.SignInt(cross1);
        int s2 = Utils.SignInt(cross2);
        int s3 = Utils.SignInt(cross3);
        int s4 = Utils.SignInt(cross4);

        return s1 == s2 && s2 == s3 && s3 == s4;
    }

    public readonly void GetFrontPoints(out Vector2 p1, out Vector2 p2, out Vector2 p3, out Vector2 p4,
        float kickerDepth = KickerDepth)
    {
        var a = Angle;
        var ah = HalfArcAngle;
        var r = Radius;

        p1 = Center + (a + ah).ToUnitVec() * r - a.ToUnitVec() * kickerDepth * 0.5f;
        p2 = Center + (a - ah).ToUnitVec() * r - a.ToUnitVec() * kickerDepth * 0.5f;
        p3 = Center + (a - ah).ToUnitVec() * r + a.ToUnitVec() * kickerDepth;
        p4 = Center + (a + ah).ToUnitVec() * r + a.ToUnitVec() * kickerDepth;
    }

    public readonly LineSegment GetFrontLine()
    {
        Vector2 p1 = Center + (Angle + HalfArcAngle).ToUnitVec() * Radius;
        Vector2 p2 = Center + (Angle - HalfArcAngle).ToUnitVec() * Radius;
        return new LineSegment(p1, p2);
    }

    private static float Cross(Vector2 a, Vector2 b) => a.X * b.Y - a.Y * b.X;
}