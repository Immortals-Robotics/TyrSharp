namespace Tyr.Common.Data.Ssl.Gc;

/// <summary>
/// MatchType is a meta information about the current match for easier log processing.
/// </summary>
public enum MatchType
{
    /// <summary> Not set. </summary>
    Unknown = 0,
    /// <summary> Match is part of the group phase. </summary>
    GroupPhase = 1,
    /// <summary> Match is part of the elimination phase. </summary>
    EliminationPhase = 2,
    /// <summary> A friendly match, not part of a tournament. </summary>
    Friendly = 3
}
