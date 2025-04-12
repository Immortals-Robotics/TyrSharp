using ProtoBuf;
using Tyr.Common.Math;

namespace Tyr.Common.Shape;

[ProtoContract]
public struct Circle(Vector2 center, float radius) : IShape
{
    [ProtoMember(1)] public Vector2 Center { get; set; } = center;
    [ProtoMember(2)] public float Radius { get; set; } = radius;

    public readonly float Circumference => 2f * MathF.PI * Radius;

    public readonly float Area => MathF.PI * Radius * Radius;

    public readonly float Distance(Vector2 point)
    {
        return (Center - point).Length() - Radius;
    }

    public readonly bool Inside(Vector2 point, float margin = 0f)
    {
        return Distance(point) < margin;
    }

    public readonly Vector2 NearestOutside(Vector2 point, float extraRadius = 0f)
    {
        var direction = point - Center;
        if (direction == Vector2.Zero)
            return Center + new Vector2(Radius + extraRadius, 0f);

        return Center + direction.Normalized() * (Radius + extraRadius);
    }

    public readonly List<Vector2> Intersect(Circle other)
    {
        var result = new List<Vector2>();

        var dVec = other.Center - Center;
        var dMag = dVec.Length();

        if (dMag > Radius + other.Radius || dMag < MathF.Abs(Radius - other.Radius))
            return result; // no intersection

        var dNormal = dVec.Normalized();

        var a = (Radius * Radius + dMag * dMag - other.Radius * other.Radius) / (2f * dMag);
        var arg = Radius * Radius - a * a;
        var h = arg > 0f ? MathF.Sqrt(arg) : 0f;

        var p2 = Center + dNormal * a;

        Vector2 point1 = new(p2.X - h * dNormal.Y, p2.Y + h * dNormal.X);
        Vector2 point2 = new(p2.X + h * dNormal.Y, p2.Y - h * dNormal.X);

        if (arg < 0f)
            return result;
        else if (arg == 0f)
            return new() { point1 };
        else
            return new() { point1, point2 };
    }

    public readonly float IntersectionArea(Circle other)
    {
        var dVec = other.Center - Center;
        var dMag = dVec.Length();

        if (dMag > Radius + other.Radius)
            return 0f;

        if (dMag <= MathF.Abs(Radius - other.Radius))
        {
            var minR = MathF.Min(Radius, other.Radius);
            return MathF.PI * minR * minR;
        }

        var intersection = Intersect(other);
        if (intersection.Count != 2)
            return 0f;

        var intMid = (intersection[0] + intersection[1]) * 0.5f;
        var d = intersection[0].DistanceTo(intMid);

        var area = 0f;

        {
            var h = intMid.DistanceTo(Center);
            var ang = MathF.Asin(d / Radius);
            area += ang * Radius * Radius;
            area -= d * h;
        }

        {
            var h = intMid.DistanceTo(other.Center);
            var ang = MathF.Asin(d / other.Radius);
            area += ang * other.Radius * other.Radius;
            area -= d * h;
        }

        return area;
    }

    public readonly bool IsCircleCross(Vector2 point1, Vector2 point2)
    {
        float x1 = point1.X, y1 = point1.Y;
        float x2 = point2.X, y2 = point2.Y;

        var a = -(y2 - y1);
        var b = x2 - x1;
        var c = -(a * x1 + b * y1);

        var distance = MathF.Abs(a * Center.X + b * Center.Y + c) / MathF.Sqrt(a * a + b * b);
        if (distance > Radius)
            return false;

        var vA = -b;
        var vB = a;
        var vC = -vB * Center.Y - vA * Center.X;

        var val1 = vA * x1 + vB * y1 + vC;
        var val2 = vA * x2 + vB * y2 + vC;

        if (val1 >= 0 && val2 >= 0)
            return Inside(point1) || Inside(point2);
        else if (val1 <= 0 && val2 <= 0)
            return Inside(point1) || Inside(point2);
        else
            return true;
    }
}