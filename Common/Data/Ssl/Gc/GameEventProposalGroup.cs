using ProtoBuf;

namespace Tyr.Common.Data.Ssl.Gc;

[ProtoContract]
public class GameEventProposalGroup
{
    [ProtoMember(3)] public required string Id { get; set; }

    [ProtoMember(1)] public List<GameEvent> GameEvents { get; set; } = [];

    [ProtoMember(2)] public bool? Accepted { get; set; }
}