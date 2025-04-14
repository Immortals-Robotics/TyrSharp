using ProtoBuf;

namespace Tyr.Common.Data.Ssl;

[ProtoContract]
public record struct RobotId
{
    [ProtoMember(1)] public uint? Id { get; set; }
    [ProtoMember(2)] public TeamColor? Team { get; set; }
}