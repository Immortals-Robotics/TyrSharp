using System.Numerics;
using Tyr.Common.Math;

namespace Tyr.Common.Shapes;

public struct Line(float a, float b, float c)
{
    // ay + bx + c = 0
    public float A { get; init; } = a;
    public float B { get; init; } = b;
    public float C { get; init; } = c;

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

        var lineC = -lineA * b.Y - lineB * b.X;
        return new Line { A = lineA, B = lineB, C = lineC };
    }

    public static Line FromPointAndAngle(Vector2 point, Angle angle)
    {
        return FromTwoPoints(point, point + angle.ToUnitVec());
    }

    public static Line FromSlopeAndIntercept(float slope, float intercept)
    {
        return new Line(1f, -slope, -intercept);
    }

    public static Line FromSegment(LineSegment segment)
    {
        return FromTwoPoints(segment.Start, segment.End);
    }

    public readonly float Slope => Utils.ApproximatelyZero(A) ? float.PositiveInfinity : -B / A;
    public readonly Vector2 Direction => new(-B, A);
    public readonly Angle Angle => Angle.FromVector(Direction);

    public readonly float Intercept => Utils.ApproximatelyZero(A) ? float.NaN : -C / A;

    // a point that is guaranteed to be on the line
    public Vector2 SomePoint
    {
        get
        {
            if (!Utils.ApproximatelyZero(A))
                return new Vector2(0f, Y(0f));
            if (!Utils.ApproximatelyZero(B))
                return new Vector2(X(0f), 0f);

            return Vector2.NaN;
        }
    }

    public readonly float Y(float x)
    {
        if (Utils.ApproximatelyZero(A))
            return float.NaN;

        return -(B * x + C) / A;
    }

    public readonly float X(float y)
    {
        if (Utils.ApproximatelyZero(B))
            return float.NaN;

        return -(A * y + C) / B;
    }

    public static List<float> AbcFormula(float a, float b, float c)
    {
        var discr = b * b - 4 * a * c;

        if (MathF.Abs(discr) < 1e-6f)
            return new() { -b / (2 * a) };

        if (discr < 0)
            return new();

        var sqrt = MathF.Sqrt(discr);
        return new() { (-b + sqrt) / (2 * a), (-b - sqrt) / (2 * a) };
    }

    public readonly Vector2? Intersect(Line other)
    {
        if (MathF.Abs(Slope - other.Slope) < 1e-6f)
            return null;

        if (MathF.Abs(A) < 1e-6f)
        {
            var x = -C / B;
            return new Vector2(x, other.Y(x));
        }
        else if (MathF.Abs(other.A) < 1e-6f)
        {
            var x = -other.C / other.B;
            return new Vector2(x, Y(x));
        }
        else
        {
            var denom = other.A * B - A * other.B;
            if (MathF.Abs(denom) < 1e-6f)
                return null;

            var x = (A * other.C - other.A * C) / denom;
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
        var minX = MathF.Min(segment.Start.X, segment.End.X);
        var maxX = MathF.Max(segment.Start.X, segment.End.X);
        var minY = MathF.Min(segment.Start.Y, segment.End.Y);
        var maxY = MathF.Max(segment.Start.Y, segment.End.Y);

        var withinX = p.X >= minX && p.X <= maxX;
        var withinY = p.Y >= minY && p.Y <= maxY;

        return (withinX && withinY) ? p : null;
    }

    public readonly List<Vector2> Intersect(Circle circle)
    {
        var result = new List<Vector2>();

        if (MathF.Abs(A) < 1e-6f)
        {
            var x = -C / B;
            var dx = x - circle.Center.X;
            var dySquared = circle.Radius * circle.Radius - dx * dx;

            if (dySquared < 0) return result;

            var dy = MathF.Sqrt(dySquared);
            result.Add(new Vector2(x, circle.Center.Y + dy));
            if (dy > 0f)
                result.Add(new Vector2(x, circle.Center.Y - dy));

            return result;
        }

        var da = -B / A;
        var db = -C / A;

        var cx = circle.Center.X;
        var cy = circle.Center.Y;
        var r2 = circle.Radius * circle.Radius;

        var dA = 1 + da * da;
        var dB = 2 * (da * db - cx - cy * da);
        var dC = cx * cx + db * db - 2 * cy * db + cy * cy - r2;

        var xs = AbcFormula(dA, dB, dC);
        foreach (var x in xs)
        {
            var y = da * x + db;
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

    public float Distance(Vector2 point)
    {
        var closest = ClosestPoint(point);
        return Vector2.Distance(point, closest);
    }
}