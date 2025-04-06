using ProtoBuf;

namespace Tyr.Common.Math;

[ProtoContract]
public struct Angle
{
    private float _deg;

    [ProtoMember(1)]
    public float Deg
    {
        readonly get => _deg;
        private set
        {
            _deg = value;
            Normalize();
        }
    }

    private const float Deg2Rad = MathF.PI / 180f;
    private const float Rad2Deg = 180f / MathF.PI;

    public Angle(float deg)
    {
        Deg = deg;
    }

    public static Angle FromDeg(float deg) => new Angle(deg);

    public static Angle FromRad(float rad) => new Angle(rad * Rad2Deg);

    public static Angle FromVector(Vector2 v)
    {
        if (v == Vector2.Zero) return FromDeg(0);
        float angle = MathF.Atan2(v.Y, v.X) * Rad2Deg;
        return FromDeg(angle);
    }

    public readonly float Rad => Deg * Deg2Rad;
    public readonly float Deg360 => (Deg + 360f) % 360f;

    public readonly float Sin() => MathF.Sin(Rad);
    public readonly float Cos() => MathF.Cos(Rad);
    public float Tan() => MathF.Tan(Rad);

    public Vector2 ToUnitVec()
    {
        float rad = Rad;
        return new Vector2(MathF.Cos(rad), MathF.Sin(rad));
    }

    public bool IsBetween(Angle a, Angle b)
    {
        float ang = Deg360;
        float a360 = a.Deg360;
        float b360 = b.Deg360;

        float min = MathF.Min(a360, b360);
        float max = MathF.Max(a360, b360);

        return ang > min && ang < max;
    }

    public static Angle Average(Angle a, Angle b)
    {
        Vector2 avg = (a.ToUnitVec() + b.ToUnitVec()) / 2f;
        return FromVector(avg);
    }

    public static Angle operator +(Angle a, Angle b) => FromDeg(a.Deg + b.Deg);
    public static Angle operator -(Angle a, Angle b) => FromDeg(a.Deg - b.Deg);
    public static Angle operator -(Angle a) => FromDeg(-a.Deg);
    public static Angle operator *(Angle a, float f) => FromDeg(a.Deg * f);
    public static Angle operator /(Angle a, float f) => FromDeg(a.Deg / f);

    public static bool operator <(Angle a, Angle b) => (b - a).Deg > 0;
    public static bool operator >(Angle a, Angle b) => (b - a).Deg < 0;

    private void Normalize()
    {
        Deg = MathF.IEEERemainder(Deg, 360f);
    }

    public override string ToString() => $"{Deg:0.##} deg";
}