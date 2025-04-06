using ProtoBuf;
using Tyr.Common.Math;
using Tyr.Common.Time;

namespace Tyr.Common.Data.Ssl.Gc;

[ProtoContract]
public class Referee
{
    [ProtoMember(18)] public required string SourceIdentifier { get; set; }

    [ProtoMember(19)] public MatchType MatchType { get; set; } = MatchType.UnknownMatch;

    [ProtoMember(1)] public ulong PacketTimestampMicroseconds { get; set; }
    public DateTime PacketTimestamp => UnixTime.FromMicroseconds((long)PacketTimestampMicroseconds);

    [ProtoMember(2)] public Stage Stage { get; set; }

    [ProtoMember(3)] public long? StageTimeLeft { get; set; }

    [ProtoMember(4)] public Command Command { get; set; }

    [ProtoMember(5)] public uint CommandCounter { get; set; }

    [ProtoMember(6)] public ulong CommandTimestampMicroseconds { get; set; }
    public DateTime CommandTimestamp => UnixTime.FromMicroseconds((long)CommandTimestampMicroseconds);

    [ProtoMember(7)] public required TeamInfo Yellow { get; set; }

    [ProtoMember(8)] public required TeamInfo Blue { get; set; }

    [ProtoMember(9)] public required Vector2 DesignatedPosition { get; set; }

    [ProtoMember(10)] public bool? BlueTeamOnPositiveHalf { get; set; }

    [ProtoMember(12)] public Command? NextCommand { get; set; }

    [ProtoMember(16)] public List<GameEvent> GameEvents { get; set; } = [];

    [ProtoMember(17)] public List<GameEventProposalGroup> GameEventProposals { get; set; } = [];

    [ProtoMember(15)] public long? CurrentActionTimeRemaining { get; set; }

    [ProtoMember(20)] public required string StatusMessage { get; set; }
}