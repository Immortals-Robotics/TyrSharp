using System.Numerics;
using Tyr.Common.Math.Shapes;

namespace Tyr.Common.Math;

public static class Geometry
{
    public static (Vector2?, Vector2?) Intersection(Circle circle1, Circle circle2)
    {
        var dVec = circle2.Center - circle1.Center;
        var dMag = dVec.Length();

        if (dMag > circle1.Radius + circle2.Radius || dMag <= MathF.Abs(circle1.Radius - circle2.Radius))
            return (null, null); // no intersection

        var dNormal = Vector2.Normalize(dVec);

        var a = (circle1.Radius * circle1.Radius + dMag * dMag - circle2.Radius * circle2.Radius) / (2f * dMag);
        var arg = circle1.Radius * circle1.Radius - a * a;
        var h = arg > 0f ? MathF.Sqrt(arg) : 0f;

        var p2 = circle1.Center + dNormal * a;

        Vector2 point1 = new(p2.X - h * dNormal.Y, p2.Y + h * dNormal.X);
        Vector2 point2 = new(p2.X + h * dNormal.Y, p2.Y - h * dNormal.X);

        if (arg < 0f)
            return (null, null);
        else if (arg == 0f)
            return (point1, null);
        else
            return (point1, point2);
    }

    public static Vector2? Intersection(Line line1, Line line2)
    {
        if (line1.IsParallelTo(line2)) return null;

        if (Utils.ApproximatelyZero(line1.A))
        {
            var x = -line1.C / line1.B;
            return new Vector2(x, line2.Y(x));
        }
        else if (Utils.ApproximatelyZero(line2.A))
        {
            var x = -line2.C / line2.B;
            return new Vector2(x, line1.Y(x));
        }
        else
        {
            var denom = line2.A * line1.B - line1.A * line2.B;
            if (Utils.ApproximatelyZero(denom))
                return null;

            var x = (line1.A * line2.C - line2.A * line1.C) / denom;
            return new Vector2(x, line1.Y(x));
        }
    }

    public static Vector2? Intersection(Line line, LineSegment segment)
    {
        var segmentLine = Line.FromTwoPoints(segment.Start, segment.End);
        var point = Intersection(line, segmentLine);
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

    public static (Vector2?, Vector2?) Intersection(Line line, Circle circle)
    {
        if (Utils.ApproximatelyZero(line.A))
        {
            var x = -line.C / line.B;
            var dx = x - circle.Center.X;
            var dySquared = circle.Radius * circle.Radius - dx * dx;

            if (dySquared < 0) return (null, null);

            var dy = MathF.Sqrt(dySquared);
            var p1 = new Vector2(x, circle.Center.Y + dy);

            Vector2? p2 = Utils.ApproximatelyZero(dy) ? null : new Vector2(x, circle.Center.Y - dy);

            return (p1, p2);
        }
        else
        {
            var da = -line.B / line.A;
            var db = -line.C / line.A;

            var cx = circle.Center.X;
            var cy = circle.Center.Y;
            var r2 = circle.Radius * circle.Radius;

            var dA = 1 + da * da;
            var dB = 2 * (da * db - cx - cy * da);
            var dC = cx * cx + db * db - 2 * cy * db + cy * cy - r2;

            var (x1, x2) = Utils.QuadraticRoots(dA, dB, dC);

            Vector2? p1 = x1.HasValue ? new Vector2(x1.Value, da * x1.Value + db) : null;
            Vector2? p2 = x2.HasValue ? new Vector2(x2.Value, da * x2.Value + db) : null;

            return (p1, p2);
        }
    }

    public static (Vector2?, Vector2?) Intersection(Rect rect, Line line)
    {
        var (e1, e2, e3, e4) = rect.Edges();

        // at most two of these are valid
        var p1 = Intersection(line, e1);
        var p2 = Intersection(line, e2);
        var p3 = Intersection(line, e3);
        var p4 = Intersection(line, e4);

        // Try each point in order until we find the first valid one
        var result1 = p1 ?? (p2 ?? (p3 ?? p4));

        if (!result1.HasValue) return (null, null);

        // Try to find a second, distinct intersection point
        bool ValidSecondPoint(Vector2? point) =>
            point.HasValue && !Utils.ApproximatelyEqual(result1.Value, point.Value);

        var result2 =
            ValidSecondPoint(p1) ? p1 :
            ValidSecondPoint(p2) ? p2 :
            ValidSecondPoint(p3) ? p3 :
            ValidSecondPoint(p4) ? p4 :
            null;

        return (result1, result2);
    }

    public static float IntersectionArea(Circle circle1, Circle circle2)
    {
        float r1 = circle1.Radius, r2 = circle2.Radius;
        float d = Vector2.Distance(circle1.Center, circle2.Center);

        if (d >= r1 + r2)
            return 0f; // No overlap

        if (d <= MathF.Abs(r1 - r2))
        {
            // One circle is completely inside the other
            float minR = MathF.Min(r1, r2);
            return MathF.PI * minR * minR;
        }

        float r1Sq = r1 * r1;
        float r2Sq = r2 * r2;

        float alpha = MathF.Acos((r1Sq + d * d - r2Sq) / (2 * r1 * d)) * 2;
        float beta = MathF.Acos((r2Sq + d * d - r1Sq) / (2 * r2 * d)) * 2;

        float area1 = 0.5f * r1Sq * (alpha - MathF.Sin(alpha));
        float area2 = 0.5f * r2Sq * (beta - MathF.Sin(beta));

        return area1 + area2;
    }

    public static bool HasIntersection(Rect rect1, Rect rect2)
    {
        return rect1.Min.X <= rect2.Max.X && rect1.Max.X >= rect2.Min.X &&
               rect1.Min.Y <= rect2.Max.Y && rect1.Max.Y >= rect2.Min.Y;
    }

    public static bool HasIntersection(Circle circle, LineSegment segment)
    {
        // TODO: simplify

        float x1 = segment.Start.X, y1 = segment.Start.Y;
        float x2 = segment.End.X, y2 = segment.End.Y;

        var a = -(y2 - y1);
        var b = x2 - x1;
        var c = -(a * x1 + b * y1);

        var distance = MathF.Abs(a * circle.Center.X + b * circle.Center.Y + c) / MathF.Sqrt(a * a + b * b);
        if (distance > circle.Radius)
            return false;

        var vA = -b;
        var vB = a;
        var vC = -vB * circle.Center.Y - vA * circle.Center.X;

        var val1 = vA * x1 + vB * y1 + vC;
        var val2 = vA * x2 + vB * y2 + vC;

        if (val1 >= 0 && val2 >= 0)
            return circle.Inside(segment.Start) || circle.Inside(segment.End);
        else if (val1 <= 0 && val2 <= 0)
            return circle.Inside(segment.Start) || circle.Inside(segment.End);
        else
            return true;
    }
}