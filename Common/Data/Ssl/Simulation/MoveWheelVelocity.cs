using ProtoBuf;

namespace Tyr.Common.Data.Ssl.Simulation;

[ProtoContract]
public readonly struct MoveWheelVelocity
{
    [ProtoMember(1, IsRequired = true)] public float FrontRight { get; init; }
    [ProtoMember(2, IsRequired = true)] public float BackRight { get; init; }
    [ProtoMember(3, IsRequired = true)] public float BackLeft { get; init; }
    [ProtoMember(4, IsRequired = true)] public float FrontLeft { get; init; }
}