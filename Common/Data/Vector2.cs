using ProtoBuf;

namespace Tyr.Common.Data;

[ProtoContract]
public struct Vector2
{
    [ProtoMember(1, IsRequired = true)] public float X { get; set; }
    [ProtoMember(2, IsRequired = true)] public float Y { get; set; }

    public static implicit operator Vector2(System.Numerics.Vector2 v) => new() { X = v.X, Y = v.Y };
    public static implicit operator System.Numerics.Vector2(Vector2 v) => new(v.X, v.Y);
}