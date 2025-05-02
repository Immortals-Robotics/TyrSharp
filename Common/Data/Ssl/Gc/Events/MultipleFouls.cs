using ProtoBuf;

namespace Tyr.Common.Data.Ssl.Gc.Events;

/// A team collected multiple fouls, which results in a yellow card
[ProtoContract]
public class MultipleFouls
{
    /// The team that collected multiple fouls
    [ProtoMember(1, IsRequired = true)] public TeamColor ByTeam { get; set; }
    
    /// The list of game events that caused the multiple fouls
    [ProtoMember(2)] public List<GameEvent> CausedGameEvents { get; set; } = [];
}