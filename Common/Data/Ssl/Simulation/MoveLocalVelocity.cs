using ProtoBuf;

namespace Tyr.Common.Data.Ssl.Simulation;

[ProtoContract]
public readonly struct MoveLocalVelocity
{
    [ProtoMember(1, IsRequired = true)] public float Forward { get; init; }
    [ProtoMember(2, IsRequired = true)] public float Left { get; init; }

    [ProtoMember(3, IsRequired = true)] public float AngularRad { get; init; }
    public Math.Angle Angular => Math.Angle.FromRad(AngularRad);
}