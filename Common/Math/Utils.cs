using System.Numerics;

namespace Tyr.Common.Math;

public static class Utils
{
    public static bool ApproximatelyZero(float a, float epsilon = 0.001f) => MathF.Abs(a) <= epsilon;

    public static bool ApproximatelyEqual(float a, float b, float epsilon = 0.001f) =>
        ApproximatelyZero(a - b, epsilon);

    public static bool ApproximatelyEqual(Vector2 a, Vector2 b) =>
        ApproximatelyZero(Vector2.DistanceSquared(a, b));

    public static int SignInt(float value) => value == 0f ? 0 : value > 0f ? 1 : -1;

    // Find the roots of a quadratic equation ax2+bx+c=0
    public static (float?, float?) QuadraticRoots(float a, float b, float c)
    {
        var discr = b * b - 4 * a * c;

        if (ApproximatelyZero(discr))
            return (-b / (2 * a), null);

        if (discr < 0)
            return (null, null);

        var sqrt = MathF.Sqrt(discr);
        return ((-b + sqrt) / (2 * a), (-b - sqrt) / (2 * a));
    }
}