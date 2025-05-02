using ProtoBuf;

namespace Tyr.Common.Data.Ssl.Gc;

/// <summary>
/// Information about a single team.
/// </summary>
[ProtoContract]
public struct TeamInfo
{
    public TeamInfo()
    {
    }

    /// <summary> The team's name (empty string if operator has not typed anything). </summary>
    [ProtoMember(1)] public string Name { get; set; } = "";

    /// <summary> The number of goals scored by the team during normal play and overtime. </summary>
    [ProtoMember(2)] public uint Score { get; set; } = 0;

    /// <summary> The number of red cards issued to the team since the beginning of the game. </summary>
    [ProtoMember(3)] public uint RedCards { get; set; } = 0;

    /// <summary>
    /// The amount of time (in microseconds) left on each yellow card issued to the team.
    /// If no yellow cards are issued, this array has no elements.
    /// Otherwise, times are ordered from smallest to largest.
    /// </summary>
    [ProtoMember(4)] public List<uint> YellowCardTimes { get; set; } = [];

    /// <summary> The total number of yellow cards ever issued to the team. </summary>
    [ProtoMember(5)] public uint YellowCards { get; set; } = 0;

    /// <summary>
    /// The number of timeouts this team can still call.
    /// If in a timeout right now, that timeout is excluded.
    /// </summary>
    [ProtoMember(6)] public uint Timeouts { get; set; } = 0;

    /// <summary> The number of microseconds of timeout this team can use. </summary>
    [ProtoMember(7)] public uint TimeoutTime { get; set; } = 0;

    /// <summary> The pattern number of this team's goalkeeper. </summary>
    [ProtoMember(8)] public uint Goalkeeper { get; set; } = 0;

    /// <summary> The total number of countable fouls that act towards yellow cards. </summary>
    [ProtoMember(9)] public uint? FoulCounter { get; set; } = null;

    /// <summary> The number of consecutive ball placement failures of this team. </summary>
    [ProtoMember(10)] public uint? BallPlacementFailures { get; set; } = null;

    /// <summary> Indicates if the team is able and allowed to place the ball. </summary>
    [ProtoMember(12)] public bool? CanPlaceBall { get; set; } = null;

    /// <summary> The maximum number of bots allowed on the field based on division and cards. </summary>
    [ProtoMember(13)] public uint? MaxAllowedBots { get; set; } = null;

    /// <summary>
    /// Indicates if the team has submitted an intent to substitute one or more robots at the next chance.
    /// </summary>
    [ProtoMember(14)] public bool? BotSubstitutionIntent { get; set; } = null;

    /// <summary>
    /// Indicates if the team reached the maximum allowed ball placement failures and is thus not allowed to place the ball anymore.
    /// </summary>
    [ProtoMember(15)] public bool? BallPlacementFailuresReached { get; set; } = null;

    /// <summary> Indicates if bot substitution is allowed. </summary>
    [ProtoMember(16)] public bool? BotSubstitutionAllowed { get; set; } = null;

    /// <summary> The number of bot substitutions left for the team. </summary>
    [ProtoMember(17)] public uint? BotSubstitutionsLeft { get; set; } = null;

    /// <summary> The time left for bot substitution in microseconds. </summary>
    [ProtoMember(18)] public uint? BotSubstitutionTimeLeft { get; set; } = null;
}
