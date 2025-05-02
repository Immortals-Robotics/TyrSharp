namespace Tyr.Common.Data.Ssl.Gc;

/// <summary>
/// These are the "fine" states of play on the field.
/// </summary>
public enum Command
{
    /// <summary> All robots should completely stop moving. </summary>
    Halt = 0,
    /// <summary> Robots must keep 50 cm from the ball. </summary>
    Stop = 1,
    /// <summary> A prepared kickoff or penalty may now be taken. </summary>
    NormalStart = 2,
    /// <summary> The ball is dropped and free for either team. </summary>
    ForceStart = 3,
    /// <summary> The yellow team may move into kickoff position. </summary>
    PrepareKickoffYellow = 4,
    /// <summary> The blue team may move into kickoff position. </summary>
    PrepareKickoffBlue = 5,
    /// <summary> The yellow team may move into penalty position. </summary>
    PreparePenaltyYellow = 6,
    /// <summary> The blue team may move into penalty position. </summary>
    PreparePenaltyBlue = 7,
    /// <summary> The yellow team may take a direct free kick. </summary>
    DirectFreeYellow = 8,
    /// <summary> The blue team may take a direct free kick. </summary>
    DirectFreeBlue = 9,
    /// <summary> The yellow team is currently in a timeout. </summary>
    TimeoutYellow = 12,
    /// <summary> The blue team is currently in a timeout. </summary>
    TimeoutBlue = 13,
    /// <summary> The yellow team just scored a goal. Deprecated: Use the score field from the team infos instead. </summary>
    GoalYellow = 14,
    /// <summary> The blue team just scored a goal. Deprecated: Use the score field from the team infos instead. </summary>
    GoalBlue = 15,
    /// <summary> Equivalent to STOP, but the yellow team must pick up the ball and drop it in the Designated Position. </summary>
    BallPlacementYellow = 16,
    /// <summary> Equivalent to STOP, but the blue team must pick up the ball and drop it in the Designated Position. </summary>
    BallPlacementBlue = 17
}
