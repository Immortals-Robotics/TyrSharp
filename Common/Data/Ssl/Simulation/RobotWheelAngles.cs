using ProtoBuf;

namespace Tyr.Common.Data.Ssl.Simulation;

[ProtoContract]
public readonly struct RobotWheelAngles
{
    [ProtoMember(1, IsRequired = true)] public float FrontRightRad { get; init; }
    [ProtoMember(2, IsRequired = true)] public float BackRightRad { get; init; }
    [ProtoMember(3, IsRequired = true)] public float BackLeftRad { get; init; }
    [ProtoMember(4, IsRequired = true)] public float FrontLeftRad { get; init; }
}