using System.Numerics;

namespace Tyr.Common.Math.Shapes;

public readonly record struct Line
{
    // ay + bx + c = 0
    public float A { get; private init; }
    public float B { get; private init; }
    public float C { get; private init; }

    public static Line FromTwoPoints(Vector2 p1, Vector2 p2)
    {
        var delta = p2 - p1;

        float a;
        float b;

        // vertical line
        if (Utils.ApproximatelyZero(delta.X))
        {
            a = 0f;
            b = 1f;
        }
        else
        {
            a = 1f;
            b = -delta.Y / delta.X;
        }

        var c = -a * p2.Y - b * p2.X;
        return new Line { A = a, B = b, C = c };
    }

    public static Line FromPointAndAngle(Vector2 point, Angle angle)
    {
        return FromTwoPoints(point, point + angle.ToUnitVec());
    }

    public static Line FromSlopeAndIntercept(float slope, float intercept) =>
        new() { A = 1f, B = -slope, C = -intercept };

    public static Line FromSegment(LineSegment segment) => FromTwoPoints(segment.Start, segment.End);

    public float Slope => Utils.ApproximatelyZero(A) ? float.PositiveInfinity : -B / A;
    public Vector2 Direction => Vector2.Normalize(new Vector2(-B, A));
    public Angle Angle => Angle.FromVector(Direction);

    public float Intercept => Utils.ApproximatelyZero(A) ? float.NaN : -C / A;

    public bool IsParallelTo(Line other) => Utils.ApproximatelyEqual(A * other.B, B * other.A);

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

    public float Y(float x) => Utils.ApproximatelyZero(A) ? float.NaN : -(B * x + C) / A;
    public float X(float y) => Utils.ApproximatelyZero(B) ? float.NaN : -(A * y + C) / B;

    public Line TangentLine(Vector2 point) => new() { A = B, B = -A, C = A * point.X - B * point.Y };

    public Vector2 ClosestPoint(Vector2 point) => Geometry.Intersection(TangentLine(point), this)!.Value;

    public float Distance(Vector2 point)
    {
        var closest = ClosestPoint(point);
        return Vector2.Distance(point, closest);
    }
}