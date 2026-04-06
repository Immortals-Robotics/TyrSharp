using System.Numerics;
using Tyr.Common.Math;

namespace Tyr.Common.Extensions;

public static class Vector2Extensions
{
    public static Angle ToAngle(this Vector2 vector2) => Angle.FromVector(vector2);

    /// <summary>
    /// Returns the angle between this vector and another vector
    /// </summary>
    public static Angle AngleWith(this Vector2 v, Vector2 other)
    {
        return (other - v).ToAngle();
    }

    /// <summary>
    /// Returns the difference between the angles of two vectors
    /// </summary>
    public static Angle AngleDiff(this Vector2 v, Vector2 other)
    {
        return other.ToAngle() - v.ToAngle();
    }


    // Returns the z-value of the cross product of two vectors.
    public static float Cross(this Vector2 v, Vector2 other)
    {
        return v.X * other.Y - v.Y * other.X;
    }

    public static Vector2 ClampMagnitude(this Vector2 v, float min, float max)
    {
        var length = v.Length();
        if (Utils.ApproximatelyZero(length)) return v;

        var clampedLength = System.Math.Clamp(length, min, max);
        return Vector2.Normalize(v) * clampedLength;
    }

    public static Vector2 WithLength(this Vector2 v, float length)
    {
        if (Utils.ApproximatelyZero(v.LengthSquared())) return Vector2.Zero;
        return Vector2.Normalize(v) * length;
    }

    public static Vector2 Rotated(this Vector2 v, Angle angle)
    {
        var rotatedAngle = v.ToAngle() + angle;
        return rotatedAngle.ToUnitVec() * v.Length();
    }

    public static Vector2 CircleAroundPoint(this Vector2 v, Angle angle, float radius)
    {
        return v + angle.ToUnitVec() * radius;
    }

    public static Vector2 PointOnConnectingLine(this Vector2 v, Vector2 other, float distance)
    {
        var direction = other - v;
        if (Utils.ApproximatelyZero(direction.LengthSquared())) return v;
        return v + Vector2.Normalize(direction) * distance;
    }

    public static Vector2 ToVector2(this NumFlat.Vec<double> v, int offset = 0) =>
        new((float)v[offset], (float)v[offset + 1]);

    public static Vector3 Xyz(this Vector2 v) => new(v.X, v.Y, 0f);
}