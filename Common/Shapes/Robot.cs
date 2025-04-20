using System.Numerics;
using Tyr.Common.Math;

namespace Tyr.Common.Shapes;

public struct Robot(Vector2 center, float radius, Angle angle)
{
    public Vector2 Center { get; } = center;
    public float Radius { get; } = radius;
    public Angle Angle { get; } = angle;

    private static readonly Angle HalfArcAngle = Angle.FromDeg(50f);
    private const float KickerDepth = 150f;

    public readonly float FrontDistance => Radius * HalfArcAngle.Cos();

    public readonly bool CanKick(Vector2 point, float kickerDepth = KickerDepth)
    {
        var p1 = Center + (Angle + HalfArcAngle).ToUnitVec() * Radius - Angle.ToUnitVec() * kickerDepth * 0.5f;
        var p2 = Center + (Angle - HalfArcAngle).ToUnitVec() * Radius - Angle.ToUnitVec() * kickerDepth * 0.5f;
        var p3 = Center + (Angle - HalfArcAngle).ToUnitVec() * Radius + Angle.ToUnitVec() * kickerDepth;
        var p4 = Center + (Angle + HalfArcAngle).ToUnitVec() * Radius + Angle.ToUnitVec() * kickerDepth;

        var v1 = point - p1;
        var v2 = point - p2;
        var v3 = point - p3;
        var v4 = point - p4;

        var cross1 = (p2 - p1).Cross(v1);
        var cross2 = (p3 - p2).Cross(v2);
        var cross3 = (p4 - p3).Cross(v3);
        var cross4 = (p1 - p4).Cross(v4);

        var s1 = Utils.SignInt(cross1);
        var s2 = Utils.SignInt(cross2);
        var s3 = Utils.SignInt(cross3);
        var s4 = Utils.SignInt(cross4);

        return s1 == s2 && s2 == s3 && s3 == s4;
    }

    public readonly LineSegment GetFrontLine()
    {
        var p1 = Center + (Angle + HalfArcAngle).ToUnitVec() * Radius;
        var p2 = Center + (Angle - HalfArcAngle).ToUnitVec() * Radius;
        return new LineSegment(p1, p2);
    }

    public float Distance(Vector2 point)
    {
        var rel = point - Center;
        var dis = rel.Length();

        var start = Angle - HalfArcAngle;
        var end = Angle + HalfArcAngle;
        var inFront = rel.ToAngle().IsBetween(start, end);

        return inFront
            ? dis - FrontDistance
            : dis - Radius;
    }

    public bool Inside(Vector2 point, float margin = 0) => Distance(point) < margin;

    public Vector2 NearestOutside(Vector2 point, float margin = 0)
    {
        var distance = Distance(point);

        if (distance >= margin) return point;

        var direction = point - Center;

        var extra = margin - distance;
        var newLength = direction.Length() + extra;

        return Vector2.Normalize(direction) * newLength + Center;
    }
}