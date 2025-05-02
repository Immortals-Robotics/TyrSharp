using ProtoBuf;

namespace Tyr.Common.Data.Ssl.Gc.Events;

[ProtoContract]
public class ChallengeFlagHandled
{
    [ProtoMember(1, IsRequired = true)] public TeamColor ByTeam { get; set; }
    [ProtoMember(2, IsRequired = true)] public bool? Accepted { get; set; }
}