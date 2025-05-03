using ProtoBuf;

namespace Tyr.Common.Data.Ssl.Gc.Events;

/// A challenge, flagged recently, has been handled by the referee
[ProtoContract]
public class ChallengeFlagHandled
{
    /// The team that requested the challenge flag
    [ProtoMember(1, IsRequired = true)] public TeamColor ByTeam { get; set; }
    
    /// The challenge was accepted by the referee
    [ProtoMember(2, IsRequired = true)] public bool? Accepted { get; set; }
}