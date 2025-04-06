using ProtoBuf;
using Tyr.Common.Math;

namespace Tyr.Common.Shape;

[ProtoContract]
public struct Line(float a, float b, float c) : IShape
{
    // ay + bx + c = 0
    [ProtoMember(1)] public float A { get; init; } = a;
    [ProtoMember(2)] public float B { get; init; } = b;
    [ProtoMember(3)] public float C { get; init; } = c;

    public static Line FromTwoPoints(Vector2 a, Vector2 b)
    {
        var delta = b - a;

        float lineA;
        float lineB;

        if (MathF.Abs(delta.X) < 1e-6f)
        {
            lineA = 0f;
            lineB = 1f;
        }
        else
        {
            lineA = 1f;
            lineB = -delta.Y / delta.X;
        }

        float lineC = -lineA * b.Y - lineB * b.X;
        return new Line { A = lineA, B = lineB, C = lineC };
    }

    public static Line FromPointAndAngle(Vector2 point, float angleRad)
    {
        Vector2 dir = new Vector2(MathF.Cos(angleRad), MathF.Sin(angleRad));
        return FromTwoPoints(point, point + dir);
    }

    public static Line FromSegment(LineSegment segment)
    {
        return FromTwoPoints(segment.Start, segment.End);
    }

    public readonly float Slope => -B / A;

    public readonly float Y(float x)
    {
        if (MathF.Abs(A) < 1e-6f)
            return 0f;

        return -(B * x + C) / A;
    }

    public readonly float X(float y)
    {
        if (MathF.Abs(B) < 1e-6f)
            return 0f;

        return -(A * y + C) / B;
    }

    public static List<float> AbcFormula(float a, float b, float c)
    {
        float discr = b * b - 4 * a * c;

        if (MathF.Abs(discr) < 1e-6f)
            return new() { -b / (2 * a) };

        if (discr < 0)
            return new();

        float sqrt = MathF.Sqrt(discr);
        return new() { (-b + sqrt) / (2 * a), (-b - sqrt) / (2 * a) };
    }

    public readonly Vector2? Intersect(Line other)
    {
        if (MathF.Abs(Slope - other.Slope) < 1e-6f)
            return null;

        if (MathF.Abs(A) < 1e-6f)
        {
            float x = -C / B;
            return new Vector2(x, other.Y(x));
        }
        else if (MathF.Abs(other.A) < 1e-6f)
        {
            float x = -other.C / other.B;
            return new Vector2(x, Y(x));
        }
        else
        {
            float denom = other.A * B - A * other.B;
            if (MathF.Abs(denom) < 1e-6f)
                return null;

            float x = (A * other.C - other.A * C) / denom;
            return new Vector2(x, Y(x));
        }
    }

    public readonly Vector2? Intersect(LineSegment segment)
    {
        var segmentLine = FromTwoPoints(segment.Start, segment.End);
        var point = Intersect(segmentLine);
        if (point == null)
            return null;

        var p = point.Value;
        float minX = MathF.Min(segment.Start.X, segment.End.X);
        float maxX = MathF.Max(segment.Start.X, segment.End.X);
        float minY = MathF.Min(segment.Start.Y, segment.End.Y);
        float maxY = MathF.Max(segment.Start.Y, segment.End.Y);

        bool withinX = p.X >= minX && p.X <= maxX;
        bool withinY = p.Y >= minY && p.Y <= maxY;

        return (withinX && withinY) ? p : null;
    }

    public readonly List<Vector2> Intersect(Circle circle)
    {
        var result = new List<Vector2>();

        if (MathF.Abs(A) < 1e-6f)
        {
            float x = -C / B;
            float dx = x - circle.Center.X;
            float dySquared = circle.Radius * circle.Radius - dx * dx;

            if (dySquared < 0) return result;

            float dy = MathF.Sqrt(dySquared);
            result.Add(new Vector2(x, circle.Center.Y + dy));
            if (dy > 0f)
                result.Add(new Vector2(x, circle.Center.Y - dy));

            return result;
        }

        float da = -B / A;
        float db = -C / A;

        float cx = circle.Center.X;
        float cy = circle.Center.Y;
        float r2 = circle.Radius * circle.Radius;

        float d_a = 1 + da * da;
        float d_b = 2 * (da * db - cx - cy * da);
        float d_c = cx * cx + db * db - 2 * cy * db + cy * cy - r2;

        var xs = AbcFormula(d_a, d_b, d_c);
        foreach (var x in xs)
        {
            float y = da * x + db;
            result.Add(new Vector2(x, y));
        }

        return result;
    }

    public readonly Line TangentLine(Vector2 point)
    {
        return new Line(B, -A, A * point.X - B * point.Y);
    }

    public readonly Vector2 ClosestPoint(Vector2 point)
    {
        return TangentLine(point).Intersect(this)!.Value;
    }

    public float Circumference => float.PositiveInfinity;
    public float Area => 0f;

    public float Distance(Vector2 point)
    {
        var closest = ClosestPoint(point);
        return point.DistanceTo(closest);
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