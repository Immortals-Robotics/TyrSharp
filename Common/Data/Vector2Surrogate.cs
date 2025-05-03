using System.Numerics;
using System.Runtime.CompilerServices;
using ProtoBuf;
using ProtoBuf.Meta;

namespace Tyr.Common.Data;

[ProtoContract]
public struct Vector2Surrogate
{
#pragma warning disable CA2255 // The 'ModuleInitializer' attribute should only be used in
    [ModuleInitializer]
    internal static void Register()
    {
        RuntimeTypeModel.Default
            .Add(typeof(Vector2), false)
            .SetSurrogate(typeof(Vector2Surrogate));
    }
#pragma warning restore CA2255

    [ProtoMember(1, IsRequired = true)] public float X { get; set; }
    [ProtoMember(2, IsRequired = true)] public float Y { get; set; }

    public static implicit operator Vector2Surrogate(Vector2 v) => new() { X = v.X, Y = v.Y };
    public static implicit operator Vector2(Vector2Surrogate v) => new(v.X, v.Y);
}