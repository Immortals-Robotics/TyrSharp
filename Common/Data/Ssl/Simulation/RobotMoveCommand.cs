using ProtoBuf;

namespace Tyr.Common.Data.Ssl.Simulation;

[ProtoContract]
public readonly struct RobotMoveCommand
{
    [ProtoMember(1)] public MoveWheelVelocity? WheelVelocity { get; init; }
    [ProtoMember(2)] public MoveLocalVelocity? LocalVelocity { get; init; }
    [ProtoMember(3)] public MoveGlobalVelocity? GlobalVelocity { get; init; }
}