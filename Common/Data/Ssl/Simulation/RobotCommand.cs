using ProtoBuf;
using Tyr.Common.Math;

namespace Tyr.Common.Data.Ssl.Simulation;

[ProtoContract]
public readonly struct RobotCommand
{
    [ProtoMember(1, IsRequired = true)] public uint Id { get; init; }

    [ProtoMember(2)] public RobotMoveCommand? MoveCommand { get; init; }

    [ProtoMember(3)] public float? KickSpeed { get; init; }
    [ProtoMember(4, IsRequired = false)] public float KickAngleDeg { get; init; }
    public Angle KickAngle
    {
        get => Angle.FromDeg(KickAngleDeg);
        init => KickAngleDeg = value.Deg;
    }

    [ProtoMember(5)] public float? DribblerSpeed { get; init; }
}