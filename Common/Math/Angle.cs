using System.Numerics;
using Tomlet;
using Tomlet.Models;

namespace Tyr.Common.Math;

public readonly record struct Angle
{
    public float Rad { get; }

    static Angle()
    {
        TomletMain.RegisterMapper(
            angle => new TomlDouble(angle.Deg),
            toml => FromDeg((float)((TomlDouble)toml).Value));
    }

    private Angle(float radians)
    {
        Rad = radians;
    }

    public static Angle FromDeg(float deg) => new(float.DegreesToRadians(deg));

    public static Angle FromRad(float rad) => new(rad);

    public static Angle FromVector(Vector2 v)
    {
        if (v == Vector2.Zero) return FromDeg(0);
        var angle = MathF.Atan2(v.Y, v.X);
        return FromRad(angle);
    }

    // normalized to [-π, π]
    public float RadNormalized => MathF.IEEERemainder(Rad, 2f * MathF.PI);

    public float Deg => float.RadiansToDegrees(Rad);

    // normalized to [-180, 180]
    public float DegNormalized => float.RadiansToDegrees(RadNormalized);
    public float Deg360 => (DegNormalized + 360f) % 360f;

    public float Sin() => MathF.Sin(Rad);
    public float Cos() => MathF.Cos(Rad);
    public float Tan() => MathF.Tan(Rad);

    public Vector2 ToUnitVec()
    {
        var rad = Rad;
        return new Vector2(MathF.Cos(rad), MathF.Sin(rad));
    }

    public bool IsBetween(Angle a, Angle b)
    {
        var diffA = System.Math.Abs((a - this).DegNormalized);
        var diffB = System.Math.Abs((b - this).DegNormalized);
        var diffAb = System.Math.Abs((a - b).DegNormalized);

        return Utils.ApproximatelyEqual(diffA + diffB, diffAb);
    }

    public static Angle Average(Angle a, Angle b)
    {
        return a + (b - a) * 0.5f;
    }

    public static Angle operator +(Angle a, Angle b) => FromDeg(a.DegNormalized + b.DegNormalized);
    public static Angle operator -(Angle a, Angle b) => FromDeg(a.DegNormalized - b.DegNormalized);
    public static Angle operator -(Angle a) => FromDeg(-a.DegNormalized);
    public static Angle operator *(Angle a, float f) => FromDeg(a.DegNormalized * f);
    public static Angle operator /(Angle a, float f) => FromDeg(a.DegNormalized / f);

    public static bool operator <(Angle a, Angle b) => (b - a).DegNormalized > 0;
    public static bool operator >(Angle a, Angle b) => (b - a).DegNormalized < 0;

    public override string ToString() => $"{DegNormalized:F2}°";

    public bool Equals(Angle other) => Utils.ApproximatelyEqual(DegNormalized, other.DegNormalized);
    public override int GetHashCode() => DegNormalized.GetHashCode();
}