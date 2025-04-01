using ProtoBuf;

namespace Tyr.Common.Ssl;

[ProtoContract]
public class RobotId
{
    [ProtoMember(1)] public uint? Id { get; set; }
    [ProtoMember(2)] public Team? Team { get; set; }
}