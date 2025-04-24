using System.Numerics;
using Tyr.Common.Math;

namespace Tyr.Common.Extensions;

public static class Vector2Extensions
{
    public static Angle ToAngle(this Vector2 vector2) => Angle.FromVector(vector2);

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

    public static MathNet.Numerics.LinearAlgebra.Vector<double> AsMathNetVector(this Vector2 v)
    {
        var mathNetVector = MathNet.Numerics.LinearAlgebra.Vector<double>.Build.Dense(2);
        mathNetVector[0] = v.X;
        mathNetVector[1] = v.Y;
        return mathNetVector;
    }

    public static Vector2 ToVector2(this MathNet.Numerics.LinearAlgebra.Vector<double> v, int offset = 0) =>
        new((float)v[offset], (float)v[offset + 1]);
}