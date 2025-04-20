using System.Numerics;

namespace Tyr.Common.Math;

public readonly record struct Angle
{
    public float Deg { get; }

    private const float Deg2Rad = MathF.PI / 180f;
    private const float Rad2Deg = 180f / MathF.PI;

    private Angle(float deg)
    {
        Deg = MathF.IEEERemainder(deg, 360f);
    }

    public static Angle FromDeg(float deg) => new(deg);

    public static Angle FromRad(float rad) => new(rad * Rad2Deg);

    public static Angle FromVector(Vector2 v)
    {
        if (v == Vector2.Zero) return FromDeg(0);
        var angle = MathF.Atan2(v.Y, v.X);
        return FromRad(angle);
    }

    public float Rad => Deg * Deg2Rad;
    public float Deg360 => (Deg + 360f) % 360f;

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
        var diffA = System.Math.Abs((a - this).Deg);
        var diffB = System.Math.Abs((b - this).Deg);
        var diffAb = System.Math.Abs((a - b).Deg);

        return Utils.ApproximatelyEqual(diffA + diffB, diffAb);
    }

    public static Angle Average(Angle a, Angle b)
    {
        return a + (b - a) * 0.5f;
    }

    public static Angle operator +(Angle a, Angle b) => FromDeg(a.Deg + b.Deg);
    public static Angle operator -(Angle a, Angle b) => FromDeg(a.Deg - b.Deg);
    public static Angle operator -(Angle a) => FromDeg(-a.Deg);
    public static Angle operator *(Angle a, float f) => FromDeg(a.Deg * f);
    public static Angle operator /(Angle a, float f) => FromDeg(a.Deg / f);

    public static bool operator <(Angle a, Angle b) => (b - a).Deg > 0;
    public static bool operator >(Angle a, Angle b) => (b - a).Deg < 0;

    public override string ToString() => $"{Deg:F2} deg";

    public bool Equals(Angle other) => Utils.ApproximatelyEqual(Deg, other.Deg);
    public override int GetHashCode() => Deg.GetHashCode();
}