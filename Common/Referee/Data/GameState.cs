namespace Tyr.Common.Referee.Data;

// https://robocup-ssl.github.io/ssl-rules/sslrules.html#_game_states
public enum GameState
{
    None = 0,
    Halt = 1,
    Timeout = 2,
    Stop = 3,
    BallPlacement = 4,
    Kickoff = 5,
    Penalty = 6,
    FreeKick = 7,
    Running = 8,
}