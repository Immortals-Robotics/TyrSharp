using System.Numerics;
using ProtoBuf;
using Tyr.Common.Time;

namespace Tyr.Common.Data.Ssl.Gc;

/// <summary>
/// Each UDP packet contains one of these messages.
/// </summary>
[ProtoContract]
public class Referee
{
    /// <summary>
    /// A random UUID of the source that is kept constant at the source while running.
    /// If multiple sources are broadcasting to the same network, this id can be used to identify individual sources.
    /// </summary>
    [ProtoMember(18)] public string SourceIdentifier { get; set; } = "";

    /// <summary>
    /// The match type is a meta information about the current match that helps to process the logs after a competition.
    /// </summary>
    [ProtoMember(19)] public MatchType MatchType { get; set; } = MatchType.Unknown;

    /// <summary>
    /// The UNIX timestamp when the packet was sent, in microseconds.
    /// Divide by 1,000,000 to get a time_t.
    /// </summary>
    [ProtoMember(1)] public ulong PacketTimestampMicroseconds { get; set; }
    public Timestamp PacketTimestamp => Timestamp.FromMicroseconds((long)PacketTimestampMicroseconds);

    /// <summary>
    /// These are the "coarse" stages of the game.
    /// </summary>
    [ProtoMember(2)] public Stage Stage { get; set; }

    /// <summary>
    /// The number of microseconds left in the stage.
    /// If the stage runs over its specified time, this value becomes negative.
    /// </summary>
    [ProtoMember(3)] public long? StageTimeLeftMicroseconds { get; set; }
    public DeltaTime StageTimeLeft => DeltaTime.FromMicroseconds(StageTimeLeftMicroseconds.GetValueOrDefault());

    /// <summary>
    /// These are the "fine" states of play on the field.
    /// </summary>
    [ProtoMember(4)] public Command Command { get; set; }

    /// <summary>
    /// The number of commands issued since startup (mod 2^32).
    /// </summary>
    [ProtoMember(5)] public uint CommandCounter { get; set; } = uint.MaxValue;

    /// <summary>
    /// The UNIX timestamp when the command was issued, in microseconds.
    /// This value changes only when a new command is issued, not on each packet.
    /// </summary>
    [ProtoMember(6)] public ulong CommandTimestampMicroseconds { get; set; }
    public Timestamp CommandTimestamp => Timestamp.FromMicroseconds((long)CommandTimestampMicroseconds);

    /// <summary>
    /// Information about the yellow team.
    /// </summary>
    [ProtoMember(7)] public TeamInfo Yellow { get; set; }

    /// <summary>
    /// Information about the blue team.
    /// </summary>
    [ProtoMember(8)] public TeamInfo Blue { get; set; }

    /// <summary>
    /// The coordinates of the Designated Position. These are measured in millimetres and correspond to SSL-Vision coordinates.
    /// </summary>
    [ProtoMember(9)] public Vector2 DesignatedPosition { get; set; }

    /// <summary>
    /// Information about the direction of play.
    /// True, if the blue team will have its goal on the positive x-axis of the SSL-Vision coordinate system.
    /// </summary>
    [ProtoMember(10)] public bool? BlueTeamOnPositiveHalf { get; set; }
    public TeamSide BlueTeamSide => BlueTeamOnPositiveHalf.GetValueOrDefault() ? TeamSide.Right : TeamSide.Left;
    public TeamSide YellowTeamSide => BlueTeamOnPositiveHalf.GetValueOrDefault() ? TeamSide.Left : TeamSide.Right;

    /// <summary>
    /// The command that will be issued after the current stoppage and ball placement to continue the game.
    /// </summary>
    [ProtoMember(12)] public Command? NextCommand { get; set; }

    /// <summary>
    /// All game events that were detected since the last RUNNING state.
    /// Will be cleared as soon as the game is continued.
    /// </summary>
    [ProtoMember(16)] public List<GameEvent> GameEvents { get; set; } = [];

    /// <summary>
    /// All non-finished proposed game events that may be processed next.
    /// </summary>
    [ProtoMember(17)] public List<GameEventProposalGroup> GameEventProposals { get; set; } = [];

    /// <summary>
    /// The time in microseconds that is remaining until the current action times out.
    /// The time will not be reset. It can get negative.
    /// </summary>
    [ProtoMember(15)] public long? CurrentActionTimeRemainingMicroseconds { get; set; }

    public DeltaTime CurrentActionTimeRemaining =>
        DeltaTime.FromMicroseconds(CurrentActionTimeRemainingMicroseconds.GetValueOrDefault());

    /// <summary>
    /// A status message providing additional information.
    /// </summary>
    [ProtoMember(20)] public string StatusMessage { get; set; } = "";
}
