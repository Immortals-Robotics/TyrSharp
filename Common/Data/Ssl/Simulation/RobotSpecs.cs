using ProtoBuf;

namespace Tyr.Common.Data.Ssl.Simulation;

[ProtoContract]
public struct RobotSpecs
{
    [ProtoMember(1, IsRequired = true)] public RobotId Id { get; set; }

    [ProtoMember(2)] public float? Radius { get; set; }
    [ProtoMember(3)] public float? Height { get; set; }
    [ProtoMember(4)] public float? Mass { get; set; }

    [ProtoMember(7)] public float? MaxLinearKickSpeed { get; set; }
    [ProtoMember(8)] public float? MaxChipKickSpeed { get; set; }
    [ProtoMember(9)] public float? CenterToDribbler { get; set; }

    [ProtoMember(10)] public RobotLimits? Limits { get; set; }

    [ProtoMember(13)] public RobotWheelAngles? WheelAngles { get; set; }

    [ProtoMember(14)] public List<Any>? Custom { get; set; }
}
