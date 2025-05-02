using System.Numerics;
using System.Runtime.CompilerServices;
using ProtoBuf;
using ProtoBuf.Meta;

namespace Tyr.Common.Data;

[ProtoContract]
public struct Vector3Surrogate
{
#pragma warning disable CA2255 // The 'ModuleInitializer' attribute should only be used in
    [ModuleInitializer]
    internal static void Register()
    {
        RuntimeTypeModel.Default
            .Add(typeof(Vector3), false)
            .SetSurrogate(typeof(Vector3Surrogate));
    }
#pragma warning restore CA2255

    [ProtoMember(1, IsRequired = true)] public float X { get; set; }
    [ProtoMember(2, IsRequired = true)] public float Y { get; set; }
    [ProtoMember(3, IsRequired = true)] public float Z { get; set; }

    public static implicit operator Vector3Surrogate(Vector3 v) => new() { X = v.X, Y = v.Y, Z = v.Z };
    public static implicit operator Vector3(Vector3Surrogate v) => new(v.X, v.Y, v.Z);
}