namespace Tyr.Common.Data.Referee;

public struct State
{
    // when transitioned to this state
    public Timestamp Timestamp = default;
    public GameState GameState = GameState.None;
    public bool Ready = false;
    public TeamColor Color = TeamColor.Blue;

    public Ssl.Gc.Referee Gc { get; set; } = new();

    public State()
    {
    }

    public override string ToString()
    {
        return GameState switch
        {
            // non-sided
            GameState.None or GameState.Halt or GameState.Stop or GameState.Running =>
                GameState.ToString(),

            // sided
            GameState.Timeout or GameState.BallPlacement or GameState.FreeKick =>
                $"{Color} {GameState}",

            // sided and readied
            GameState.Kickoff or GameState.Penalty =>
                $"{Color} {GameState} (ready: {Ready})",

            _ => "Unknown"
        };
    }
}