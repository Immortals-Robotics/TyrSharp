using ProtoBuf;

namespace Tyr.Common.Math;

[ProtoContract]
public struct Vector3(float x, float y, float z) : IEquatable<Vector3>
{
    // it is important to keep the tags as x: 1, y: 2, z: 3
    // so that they match the SSL ones
    [ProtoMember(1, IsRequired = true)] public float X = x;
    [ProtoMember(2, IsRequired = true)] public float Y = y;
    [ProtoMember(3, IsRequired = true)] public float Z = z;

    public static readonly Vector3 Zero = new(0, 0, 0);

    public readonly Vector3 Normalized()
    {
        var length = Length();
        return Utils.AlmostEqual(length, 0.0f) ? Zero : this / length;
    }

    public readonly float Length() => MathF.Sqrt(LengthSquared());
    public readonly float LengthSquared() => Dot(this);

    public float DistanceTo(Vector3 v) => (v - this).Length();
    public float DistanceSquaredTo(Vector3 v) => (v - this).LengthSquared();

    public readonly float Dot(Vector3 v) => X * v.X + Y * v.Y + Z * v.Z;

    public Vector3 Cross(Vector3 v) =>
        new(
            Y * v.Z - Z * v.Y,
            Z * v.X - X * v.Z,
            X * v.Y - Y * v.X
        );

    public Vector2 Xy() => new(X, Y);
    public Vector3 Abs() => new(MathF.Abs(X), MathF.Abs(Y), MathF.Abs(Z));

    public static Vector3 operator +(Vector3 a, Vector3 b) => new(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
    public static Vector3 operator -(Vector3 a, Vector3 b) => new(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
    public static Vector3 operator *(Vector3 a, Vector3 b) => new(a.X * b.X, a.Y * b.Y, a.Z * b.Z);
    public static Vector3 operator /(Vector3 a, Vector3 b) => new(a.X / b.X, a.Y / b.Y, a.Z / b.Z);
    public static Vector3 operator *(Vector3 a, float d) => new(a.X * d, a.Y * d, a.Z * d);
    public static Vector3 operator /(Vector3 a, float d) => new(a.X / d, a.Y / d, a.Z / d);
    public static Vector3 operator -(Vector3 v) => new(-v.X, -v.Y, -v.Z);
    public static Vector3 operator +(Vector3 v) => v;

    public static bool operator ==(Vector3 a, Vector3 b) => a.Equals(b);
    public static bool operator !=(Vector3 a, Vector3 b) => !a.Equals(b);

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