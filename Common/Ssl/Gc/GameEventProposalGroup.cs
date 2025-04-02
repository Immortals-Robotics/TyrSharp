using ProtoBuf;

namespace Tyr.Common.Ssl.Gc;

[ProtoContract]
public class GameEventProposalGroup
{
    [ProtoMember(3)] public string Id { get; set; }

    [ProtoMember(1)] public List<GameEvent> GameEvents { get; set; } = new List<GameEvent>();

    [ProtoMember(2)] public bool? Accepted { get; set; }
}