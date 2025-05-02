using ProtoBuf;

namespace Tyr.Common.Data.Ssl.Gc.Events;

/// A team collected multiple yellow cards
[ProtoContract]
public class MultipleCards
{
    /// The team that received multiple yellow cards
    [ProtoMember(1, IsRequired = true)] public TeamColor ByTeam { get; set; }
}