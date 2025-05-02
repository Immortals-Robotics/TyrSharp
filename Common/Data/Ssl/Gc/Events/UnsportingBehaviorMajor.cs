using ProtoBuf;

namespace Tyr.Common.Data.Ssl.Gc.Events;

[ProtoContract]
public class UnsportingBehaviorMajor
{
    [ProtoMember(1, IsRequired = true)] public TeamColor ByTeam { get; set; }
    [ProtoMember(2, IsRequired = true)] public required string Reason { get; set; }
}