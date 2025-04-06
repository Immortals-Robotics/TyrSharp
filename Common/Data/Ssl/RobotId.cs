using ProtoBuf;

namespace Tyr.Common.Data.Ssl;

[ProtoContract]
public struct RobotId
{
    [ProtoMember(1)] public uint? Id { get; set; }
    [ProtoMember(2)] public Team? Team { get; set; }
}