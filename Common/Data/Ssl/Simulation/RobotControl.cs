using ProtoBuf;

namespace Tyr.Common.Data.Ssl.Simulation;

[ProtoContract]
public struct RobotControl
{
    [ProtoMember(1)] public List<RobotCommand> RobotCommands { get; set; }
}