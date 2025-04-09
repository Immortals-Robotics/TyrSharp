using ProtoBuf;
using Tyr.Common.Config;
using Tyr.Common.Math;
using Tyr.Common.Time;

namespace Tyr.Common.Data.Ssl.Gc;

[ProtoContract]
public class Referee
{
    [ProtoMember(18)] public string SourceIdentifier { get; set; } = "";

    [ProtoMember(19)] public MatchType MatchType { get; set; } = MatchType.Unknown;

    [ProtoMember(1)] public ulong PacketTimestampMicroseconds { get; set; }
    public DateTime PacketTimestamp => UnixTime.FromMicroseconds((long)PacketTimestampMicroseconds);

    [ProtoMember(2)] public Stage Stage { get; set; }

    // in micro-seconds
    [ProtoMember(3)] public long? StageTimeLeftMicroseconds { get; set; }
    public float StageTimeLeft => Duration.FromMicroseconds(StageTimeLeftMicroseconds.GetValueOrDefault());

    [ProtoMember(4)] public Command Command { get; set; }

    [ProtoMember(5)] public uint CommandCounter { get; set; }

    [ProtoMember(6)] public ulong CommandTimestampMicroseconds { get; set; }
    public DateTime CommandTimestamp => UnixTime.FromMicroseconds((long)CommandTimestampMicroseconds);

    [ProtoMember(7)] public TeamInfo Yellow { get; set; }

    [ProtoMember(8)] public TeamInfo Blue { get; set; }

    [ProtoMember(9)] public Vector2 DesignatedPosition { get; set; }

    [ProtoMember(10)] public bool? BlueTeamOnPositiveHalf { get; set; }
    public TeamSide BlueTeamSide => BlueTeamOnPositiveHalf.GetValueOrDefault() ? TeamSide.Right : TeamSide.Left;
    public TeamSide YellowTeamSide => BlueTeamOnPositiveHalf.GetValueOrDefault() ? TeamSide.Left : TeamSide.Right;

    [ProtoMember(12)] public Command? NextCommand { get; set; }

    [ProtoMember(16)] public List<GameEvent> GameEvents { get; set; } = [];

    [ProtoMember(17)] public List<GameEventProposalGroup> GameEventProposals { get; set; } = [];

    [ProtoMember(15)] public long? CurrentActionTimeRemainingMicroseconds { get; set; }

    public float CurrentActionTimeRemaining =>
        Duration.FromMicroseconds(CurrentActionTimeRemainingMicroseconds.GetValueOrDefault());

    [ProtoMember(20)] public string StatusMessage { get; set; } = "";
}