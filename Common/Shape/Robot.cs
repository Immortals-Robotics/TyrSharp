using ProtoBuf;
using Tyr.Common.Math;
using System.Numerics;

namespace Tyr.Common.Shape;

[ProtoContract]
public struct Robot(Vector2 center, float radius, Angle angle) : IShape
{
    [ProtoMember(1)] public Vector2 Center { get; } = center;
    [ProtoMember(2)] public float Radius { get; } = radius;
    [ProtoMember(3)] public Angle Angle { get; } = angle;

    private static readonly Angle HalfArcAngle = Angle.FromDeg(50f);
    private const float KickerDepth = 150f;

    public readonly float FrontDistance() => Radius * HalfArcAngle.Cos();

    public readonly bool CanKick(Vector2 point, float kickerDepth = KickerDepth)
    {
        var a = Angle;
        var ah = HalfArcAngle;
        var r = Radius;

        var p1 = Center + (a + ah).ToUnitVec() * r - a.ToUnitVec() * kickerDepth * 0.5f;
        var p2 = Center + (a - ah).ToUnitVec() * r - a.ToUnitVec() * kickerDepth * 0.5f;
        var p3 = Center + (a - ah).ToUnitVec() * r + a.ToUnitVec() * kickerDepth;
        var p4 = Center + (a + ah).ToUnitVec() * r + a.ToUnitVec() * kickerDepth;

        var v1 = point - p1;
        var v2 = point - p2;
        var v3 = point - p3;
        var v4 = point - p4;

        var cross1 = Utils.Vector2Cross(p2 - p1, v1);
        var cross2 = Utils.Vector2Cross(p3 - p2, v2);
        var cross3 = Utils.Vector2Cross(p4 - p3, v3);
        var cross4 = Utils.Vector2Cross(p1 - p4, v4);

        var s1 = Utils.SignInt(cross1);
        var s2 = Utils.SignInt(cross2);
        var s3 = Utils.SignInt(cross3);
        var s4 = Utils.SignInt(cross4);

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
        var p1 = Center + (Angle + HalfArcAngle).ToUnitVec() * Radius;
        var p2 = Center + (Angle - HalfArcAngle).ToUnitVec() * Radius;
        return new LineSegment(p1, p2);
    }

    public float Circumference => 0f;
    public float Area => 0f;

    public float Distance(Vector2 point)
    {
        throw new NotImplementedException();
    }

    public bool Inside(Vector2 point, float margin = 0)
    {
        var rel = point - Center;
        var dis = rel.Length();

        var start = Angle - HalfArcAngle;
        var end = Angle + HalfArcAngle;

        return dis < FrontDistance() || (dis <= Radius && rel.ToAngle().IsBetween(start, end));
    }

    public Vector2 NearestOutside(Vector2 point, float margin = 0)
    {
        throw new NotImplementedException();
    }
}