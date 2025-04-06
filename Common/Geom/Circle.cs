using ProtoBuf;
using Tyr.Common.Math;

namespace Tyr.Common.Geom;

[ProtoContract]
public struct Circle(Vector2 center, float radius)
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
        Vector2 direction = point - Center;
        if (direction == Vector2.Zero)
            return Center + new Vector2(Radius + extraRadius, 0f);

        return Center + direction.Normalized() * (Radius + extraRadius);
    }

    public readonly List<Vector2> Intersect(Circle other)
    {
        var result = new List<Vector2>();

        Vector2 dVec = other.Center - Center;
        float dMag = dVec.Length();

        if (dMag > Radius + other.Radius || dMag < MathF.Abs(Radius - other.Radius))
            return result; // no intersection

        Vector2 dNormal = dVec.Normalized();

        float a = (Radius * Radius + dMag * dMag - other.Radius * other.Radius) / (2f * dMag);
        float arg = Radius * Radius - a * a;
        float h = arg > 0f ? MathF.Sqrt(arg) : 0f;

        Vector2 p2 = Center + dNormal * a;

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
        Vector2 dVec = other.Center - Center;
        float dMag = dVec.Length();

        if (dMag > Radius + other.Radius)
            return 0f;

        if (dMag <= MathF.Abs(Radius - other.Radius))
        {
            float minR = MathF.Min(Radius, other.Radius);
            return MathF.PI * minR * minR;
        }

        var intersection = Intersect(other);
        if (intersection.Count != 2)
            return 0f;

        Vector2 intMid = (intersection[0] + intersection[1]) * 0.5f;
        float d = intersection[0].DistanceTo(intMid);

        float area = 0f;

        {
            float h = intMid.DistanceTo(Center);
            float ang = MathF.Asin(d / Radius);
            area += ang * Radius * Radius;
            area -= d * h;
        }

        {
            float h = intMid.DistanceTo(other.Center);
            float ang = MathF.Asin(d / other.Radius);
            area += ang * other.Radius * other.Radius;
            area -= d * h;
        }

        return area;
    }

    public readonly bool IsCircleCross(Vector2 point1, Vector2 point2)
    {
        float x1 = point1.X, y1 = point1.Y;
        float x2 = point2.X, y2 = point2.Y;

        float a = -(y2 - y1);
        float b = x2 - x1;
        float c = -(a * x1 + b * y1);

        float distance = MathF.Abs(a * Center.X + b * Center.Y + c) / MathF.Sqrt(a * a + b * b);
        if (distance > Radius)
            return false;

        float vA = -b;
        float vB = a;
        float vC = -vB * Center.Y - vA * Center.X;

        float val1 = vA * x1 + vB * y1 + vC;
        float val2 = vA * x2 + vB * y2 + vC;

        if (val1 >= 0 && val2 >= 0)
            return Inside(point1) || Inside(point2);
        else if (val1 <= 0 && val2 <= 0)
            return Inside(point1) || Inside(point2);
        else
            return true;
    }
}