using System.Numerics;

namespace Tyr.Common.Math.Extensions;

public static class Vector2Extensions
{
    public static Angle ToAngle(this Vector2 vector2) => Angle.FromVector(vector2);

    // Returns the z-value of the cross product of two vectors.
    public static float Cross(this Vector2 v, Vector2 other)
    {
        return v.X * other.Y - v.Y * other.X;
    }
}