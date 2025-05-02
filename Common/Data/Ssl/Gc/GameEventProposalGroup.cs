using ProtoBuf;

namespace Tyr.Common.Data.Ssl.Gc;

/// <summary>
/// List of matching proposals.
/// </summary>
[ProtoContract]
public class GameEventProposalGroup
{
    /// <summary> The proposed game event. </summary>
    [ProtoMember(1)] public List<GameEvent> GameEvents { get; set; } = [];

    /// <summary> Whether the proposal group was accepted. </summary>
    [ProtoMember(2)] public bool? Accepted { get; set; }

    /// <summary> The unique identifier for the proposal group. </summary>
    [ProtoMember(3)] public required string Id { get; set; }
}
