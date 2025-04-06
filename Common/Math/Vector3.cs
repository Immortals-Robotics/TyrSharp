using ProtoBuf;

namespace Tyr.Common.Math;

[ProtoContract]
public struct Vector3(float x, float y, float z) : IEquatable<Vector3>
{
    [ProtoMember(1)] public float X = x;
    [ProtoMember(2)] public float Y = y;
    [ProtoMember(3)] public float Z = z;

    public Vector3(float f) : this(f, f, f)
    {
    }

    public Vector3 Normalized()
    {
        var length = Length();
        return Utils.AlmostEqual(length, 0.0f) ? new Vector3() : this / length;
    }

    public float Length() => MathF.Sqrt(X * X + Y * Y + Z * Z);
    public float LengthSquared() => X * X + Y * Y + Z * Z;
    public float Dot(Vector3 v) => X * v.X + Y * v.Y + Z * v.Z;
    public float DistanceTo(Vector3 v) => (v - this).Length();
    public float DistanceSquaredTo(Vector3 v) => (v - this).LengthSquared();

    public Vector2 XY() => new Vector2(X, Y);
    public Vector3 Abs() => new Vector3(MathF.Abs(X), MathF.Abs(Y), MathF.Abs(Z));

    public static Vector3 operator +(Vector3 a, Vector3 b) => new Vector3(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
    public static Vector3 operator -(Vector3 a, Vector3 b) => new Vector3(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
    public static Vector3 operator *(Vector3 a, Vector3 b) => new Vector3(a.X * b.X, a.Y * b.Y, a.Z * b.Z);
    public static Vector3 operator /(Vector3 a, Vector3 b) => new Vector3(a.X / b.X, a.Y / b.Y, a.Z / b.Z);
    public static Vector3 operator *(Vector3 a, float d) => new Vector3(a.X * d, a.Y * d, a.Z * d);
    public static Vector3 operator /(Vector3 a, float d) => new Vector3(a.X / d, a.Y / d, a.Z / d);
    public static Vector3 operator -(Vector3 v) => new Vector3(-v.X, -v.Y, -v.Z);
    public static Vector3 operator +(Vector3 v) => v;

    public static bool operator ==(Vector3 a, Vector3 b) => Equals(a, b);
    public static bool operator !=(Vector3 a, Vector3 b) => !Equals(a, b);

    public bool Equals(Vector3 other)
    {
        return Utils.AlmostEqual(X, other.X) &&
               Utils.AlmostEqual(Y, other.Y) &&
               Utils.AlmostEqual(Z, other.Z);
    }

    public override bool Equals(object? obj)
    {
        return obj is Vector3 other && Equals(other);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(X, Y, Z);
    }

    public override string ToString() => $"[{X}, {Y}, {Z}]";
}