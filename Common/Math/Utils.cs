using System.Numerics;
using System.Runtime.CompilerServices;

namespace Tyr.Common.Math;

public static class Utils
{
    public static bool ApproximatelyEqual(float a, float b, float epsilon = 0.001f) => MathF.Abs(a - b) <= epsilon;

    public static int SignInt(float value) => value == 0f ? 0 : value > 0f ? 1 : -1;

    /// <summary>
    /// Returns the z-value of the cross product of two vectors.
    /// Since the Vector2 is in the x-y plane, a 3D cross product
    /// only produces the z-value
    /// </summary>
    /// <param name="value1">The first vector.</param>
    /// <param name="value2">The second vector.</param>
    /// <returns>The value of the z-coordinate from the cross product.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Vector2Cross(Vector2 value1, Vector2 value2)
    {
        return value1.X * value2.Y
               - value1.Y * value2.X;
    }
}