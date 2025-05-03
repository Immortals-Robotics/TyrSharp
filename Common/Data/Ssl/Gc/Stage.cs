namespace Tyr.Common.Data.Ssl.Gc;

/// <summary>
/// These are the "coarse" stages of the game.
/// </summary>
public enum Stage
{
    /// <summary> Unknown stage. </summary>
    Unknown = -1,
    /// <summary> The first half is about to start. A kickoff is called within this stage. This stage ends with the NORMAL_START. </summary>
    NormalFirstHalfPre = 0,
    /// <summary> The first half of the normal game, before half time. </summary>
    NormalFirstHalf = 1,
    /// <summary> Half time between first and second halves. </summary>
    NormalHalfTime = 2,
    /// <summary> The second half is about to start. A kickoff is called within this stage. This stage ends with the NORMAL_START. </summary>
    NormalSecondHalfPre = 3,
    /// <summary> The second half of the normal game, after half time. </summary>
    NormalSecondHalf = 4,
    /// <summary> The break before extra time. </summary>
    ExtraTimeBreak = 5,
    /// <summary> The first half of extra time is about to start. A kickoff is called within this stage. This stage ends with the NORMAL_START. </summary>
    ExtraFirstHalfPre = 6,
    /// <summary> The first half of extra time. </summary>
    ExtraFirstHalf = 7,
    /// <summary> Half time between first and second extra halves. </summary>
    ExtraHalfTime = 8,
    /// <summary> The second half of extra time is about to start. A kickoff is called within this stage. This stage ends with the NORMAL_START. </summary>
    ExtraSecondHalfPre = 9,
    /// <summary> The second half of extra time. </summary>
    ExtraSecondHalf = 10,
    /// <summary> The break before penalty shootout. </summary>
    PenaltyShootoutBreak = 11,
    /// <summary> The penalty shootout. </summary>
    PenaltyShootout = 12,
    /// <summary> The game is over. </summary>
    PostGame = 13
}
