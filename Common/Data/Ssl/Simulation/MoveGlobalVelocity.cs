using ProtoBuf;

namespace Tyr.Common.Data.Ssl.Simulation;

[ProtoContract]
public readonly struct MoveGlobalVelocity
{
    [ProtoMember(1, IsRequired = true)] public float X { get; init; }
    [ProtoMember(2, IsRequired = true)] public float Y { get; init; }
    public System.Numerics.Vector2 Velocity => new(X, Y);

    [ProtoMember(3, IsRequired = true)] public float AngularRad { get; init; }
    public Math.Angle Angular => Math.Angle.FromRad(AngularRad);

}