using System.Numerics;
using ProtoBuf;

namespace Tyr.Common.Data.Ssl.Simulation;

[ProtoContract]
public readonly struct TeleportBall
{
    [ProtoMember(1)] public float? X { get; init; }
    [ProtoMember(2)] public float? Y { get; init; }
    [ProtoMember(3)] public float? Z { get; init; }

    public Vector3? Position
    {
        get => X.HasValue && Y.HasValue && Z.HasValue
            ? new Vector3(X.Value, Y.Value, Z.Value)
            : null;
        init
        {
            X = value?.X;
            Y = value?.Y;
            Z = value?.Z;
        }
    }

    [ProtoMember(4)] public float? Vx { get; init; }
    [ProtoMember(5)] public float? Vy { get; init; }
    [ProtoMember(6)] public float? Vz { get; init; }
    
    public Vector3? Velocity
    {
        get => Vx.HasValue && Vy.HasValue && Vz.HasValue
            ? new Vector3(Vx.Value, Vy.Value, Vz.Value)
            : null;
        init
        {
            Vx = value?.X;
            Vy = value?.Y;
            Vz = value?.Z;
        }
    }

    [ProtoMember(7, IsRequired = false)] public bool TeleportSafely { get; init; }
    [ProtoMember(8, IsRequired = false)] public bool Roll { get; init; }
    [ProtoMember(9, IsRequired = false)] public bool ByForce { get; init; }
}