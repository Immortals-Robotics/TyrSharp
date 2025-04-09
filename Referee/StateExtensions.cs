using Tyr.Common.Config;
using Tyr.Common.Data.Referee;
using Tyr.Common.Data.Ssl.Gc;

namespace Tyr.Referee;

public static class StateExtensions
{
    public static float Elapsed(this State state) => (float)(DateTime.UtcNow - state.Time).TotalSeconds;

    public static bool Our(this State state) => state.Color == Configs.Common.OurColor;
    public static bool Halt(this State state) => state.GameState == GameState.Halt;
    public static bool Stop(this State state) => state.GameState == GameState.Stop;
    public static bool Running(this State state) => state.GameState == GameState.Running;
    public static bool Kickoff(this State state) => state.GameState == GameState.Kickoff;
    public static bool OurKickoff(this State state) => state.Kickoff() && state.Our();
    public static bool TheirKickoff(this State state) => state.Kickoff() && !state.Our();
    public static bool PenaltyKick(this State state) => state.GameState == GameState.Penalty;
    public static bool OurPenaltyKick(this State state) => state.PenaltyKick() && state.Our();
    public static bool TheirPenaltyKick(this State state) => state.PenaltyKick() && !state.Our();
    public static bool FreeKick(this State state) => state.GameState == GameState.FreeKick;
    public static bool OurFreeKick(this State state) => state.FreeKick() && state.Our();
    public static bool TheirFreeKick(this State state) => state.FreeKick() && !state.Our();
    public static bool BallPlacement(this State state) => state.GameState == GameState.BallPlacement;
    public static bool OurBallPlacement(this State state) => state.BallPlacement() && state.Our();
    public static bool TheirBallPlacement(this State state) => state.BallPlacement() && !state.Our();

    public static bool Restart(this State state) =>
        state.GameState is GameState.Kickoff or GameState.Penalty or GameState.FreeKick;

    public static bool OurRestart(this State state) => state.Restart() && state.Our();
    public static bool TheirRestart(this State state) => state.Restart() && !state.Our();
    public static bool Timeout(this State state) => state.GameState == GameState.Timeout;
    public static bool OurTimeout(this State state) => state.Timeout() && state.Our();
    public static bool TheirTimeout(this State state) => state.Timeout() && !state.Our();

    public static bool CanMove(this State state) => !state.Halt();

    public static bool AllowedNearBall(this State state) =>
        state.Running() || state.OurRestart() || state.OurBallPlacement();

    public static bool CanKickBall(this State state) => state.Running() || (state.OurRestart() && state.Ready);

    public static bool ShouldSlowDown(this State state) =>
        state.Stop() || state.BallPlacement() || state.CanEnterField();

    public static bool CanEnterField(this State state) => state.Timeout();

    public static TeamInfo OurInfo(this State state) =>
        Configs.Common.OurColor == TeamColor.Blue ? state.Gc.Blue : state.Gc.Yellow;

    public static TeamInfo OppInfo(this State state) =>
        Configs.Common.OurColor == TeamColor.Yellow ? state.Gc.Blue : state.Gc.Yellow;

    public static TeamSide OurSide(this State state) => Configs.Common.OurColor == TeamColor.Blue
        ? state.Gc.BlueTeamSide
        : state.Gc.YellowTeamSide;
}