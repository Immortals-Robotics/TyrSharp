using ProtoBuf;

namespace Tyr.Common.Data.Ssl.Simulation;

[ProtoContract]
public readonly struct RobotFeedback
{
    [ProtoMember(1, IsRequired = true)] public uint Id { get; init; }
    [ProtoMember(2)] public bool? DribblerBallContact { get; init; }
}