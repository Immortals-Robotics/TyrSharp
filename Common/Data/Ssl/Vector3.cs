using ProtoBuf;

namespace Tyr.Common.Data.Ssl;

[ProtoContract]
public struct Vector3
{
    [ProtoMember(1, IsRequired = true)] public float X { get; set; }
    [ProtoMember(2, IsRequired = true)] public float Y { get; set; }
    [ProtoMember(3, IsRequired = true)] public float Z { get; set; }

    public static implicit operator Vector3(Math.Vector3 v) => new() { X = v.X, Y = v.Y, Z = v.Z };
    public static implicit operator Math.Vector3(Vector3 v) => new(v.X, v.Y, v.Z);
}