namespace Tyr.Common.Data.Referee;

public record State
{
    // when transitioned to this state
    public Timestamp Timestamp { get; init; }
    public GameState GameState { get; init; } = GameState.None;
    public bool Ready { get; init; }
    public TeamColor Color { get; init; } = TeamColor.Unknown;

    public Ssl.Gc.Referee Gc { get; init; } = new();

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