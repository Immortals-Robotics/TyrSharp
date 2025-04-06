using ProtoBuf;

namespace Tyr.Common.Math;

[ProtoContract]
public struct Vector2(float x, float y) : IEquatable<Vector2>
{
    // it is important to keep the tags as x: 1, y: 2
    // so that they match the SSL ones
    [ProtoMember(1, IsRequired = true)] public float X = x;
    [ProtoMember(2, IsRequired = true)] public float Y = y;

    public Vector2(float f) : this(f, f)
    {
    }

    public static readonly Vector2 Zero = new(0, 0);

    public Vector2 Normalized()
    {
        float len = Length();
        return Utils.AlmostEqual(len, 0f) ? Zero : this / len;
    }

    public float Length() => MathF.Sqrt(X * X + Y * Y);

    public float LengthSquared() => X * X + Y * Y;

    public float Dot(Vector2 v) => X * v.X + Y * v.Y;

    public float Cross(Vector2 v) => X * v.Y - Y * v.X;

    public float DistanceTo(Vector2 v) => (v - this).Length();

    public float DistanceSquaredTo(Vector2 v) => (v - this).LengthSquared();

    public Vector2 PointOnConnectingLine(Vector2 to, float dist)
    {
        float m = (to.Y - Y) / (to.X - X);
        float dx = dist / MathF.Sqrt(m * m + 1);
        float x = to.X > X ? X + dx : X - dx;
        float y = Y + m * (x - X);
        return new Vector2(x, y);
    }

    public Vector2 Abs() => new(MathF.Abs(X), MathF.Abs(Y));

    public Vector2 Rotated(Angle angle) => angle.ToUnitVec() * Length();

    public Angle ToAngle() => Angle.FromVector(this);

    public Angle AngleWith(Vector2 v) => (v - this).ToAngle();

    public Angle AngleDiff(Vector2 v) => v.ToAngle() - ToAngle();

    public Vector2 CircleAroundPoint(Angle angle, float radius) => this + angle.ToUnitVec() * radius;

    public Vector2 XX() => new(X, X);
    public Vector2 YY() => new(Y, Y);
    public Vector2 YX() => new(Y, X);

    public static Vector2 Max(Vector2 a, Vector2 b) => new(MathF.Max(a.X, b.X), MathF.Max(a.Y, b.Y));
    public static Vector2 Min(Vector2 a, Vector2 b) => new(MathF.Min(a.X, b.X), MathF.Min(a.Y, b.Y));

    public static Vector2 Clamp(Vector2 value, Vector2 min, Vector2 max)
    {
        return new(
            System.Math.Clamp(value.X, min.X, max.X),
            System.Math.Clamp(value.Y, min.Y, max.Y)
        );
    }

    public static Vector2 operator +(Vector2 a, Vector2 b) => new(a.X + b.X, a.Y + b.Y);
    public static Vector2 operator -(Vector2 a, Vector2 b) => new(a.X - b.X, a.Y - b.Y);
    public static Vector2 operator *(Vector2 a, Vector2 b) => new(a.X * b.X, a.Y * b.Y);
    public static Vector2 operator /(Vector2 a, Vector2 b) => new(a.X / b.X, a.Y / b.Y);
    public static Vector2 operator *(Vector2 v, float f) => new(v.X * f, v.Y * f);
    public static Vector2 operator /(Vector2 v, float f) => new(v.X / f, v.Y / f);
    public static Vector2 operator *(float f, Vector2 v) => v * f;
    public static Vector2 operator -(Vector2 v) => new(-v.X, -v.Y);

    public static bool operator ==(Vector2 a, Vector2 b) => Equals(a, b);
    public static bool operator !=(Vector2 a, Vector2 b) => !Equals(a, b);

    public bool Equals(Vector2 other)
    {
        return Utils.AlmostEqual(X, other.X) &&
               Utils.AlmostEqual(Y, other.Y);
    }

    public override bool Equals(object? obj)
    {
        return obj is Vector2 other && Equals(other);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(X, Y);
    }

    public override string ToString() => $"[{X}, {Y}]";
}