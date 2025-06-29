using ProtoBuf;

namespace Tyr.Common.Data.Ssl.Simulation;

[ProtoContract]
public readonly struct RobotLimits
{
    [ProtoMember(1)] public float? AccSpeedupAbsoluteMax { get; init; }
    [ProtoMember(2)] public float? AccSpeedupAngularMax { get; init; }
    [ProtoMember(3)] public float? AccBrakeAbsoluteMax { get; init; }
    [ProtoMember(4)] public float? AccBrakeAngularMax { get; init; }
    [ProtoMember(5)] public float? VelAbsoluteMax { get; init; }
    [ProtoMember(6)] public float? VelAngularMax { get; init; }
}