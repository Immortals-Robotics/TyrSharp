using Tyr.Common.Config;
using Tyr.Common.Data.Ssl.Gc;
using Tyr.Common.Math;
using MatchType = Tyr.Common.Data.Ssl.Gc.MatchType;

namespace Tyr.Common.Data.Referee;

public struct State
{
    // when transitioned to this state
    public DateTime Time = default;

    public MatchType MatchType = MatchType.Unknown;

    public Stage Stage = Stage.Unknown;
    public float StageTimeLeft = 0f;

    public GameState GameState = GameState.None;
    public bool Ready = false;
    public TeamColor Color = TeamColor.Blue;
    public float TimeRemaining = 0f;

    public Command LastCommand = Command.Halt;

    public Vector2 DesignatedPosition = default;

    public TeamInfo BlueInfo = new();
    public TeamInfo YellowInfo = new();

    public TeamSide OurSide = TeamSide.Left;

    public string StatusMessage = "";

    public State()
    {
    }

    public bool Our() => Color == Config.Common.OurColor;
    public bool Halt() => GameState == GameState.Halt;
    public bool Stop() => GameState == GameState.Stop;
    public bool Running() => GameState == GameState.Running;
    public bool Kickoff() => GameState == GameState.Kickoff;
    public bool OurKickoff() => Kickoff() && Our();
    public bool TheirKickoff() => Kickoff() && !Our();
    public bool PenaltyKick() => GameState == GameState.Penalty;
    public bool OurPenaltyKick() => PenaltyKick() && Our();
    public bool TheirPenaltyKick() => PenaltyKick() && !Our();
    public bool FreeKick() => GameState == GameState.FreeKick;
    public bool OurFreeKick() => FreeKick() && Our();
    public bool TheirFreeKick() => FreeKick() && !Our();
    public bool BallPlacement() => GameState == GameState.BallPlacement;
    public bool OurBallPlacement() => BallPlacement() && Our();
    public bool TheirBallPlacement() => BallPlacement() && !Our();
    public bool Restart() => GameState is GameState.Kickoff or GameState.Penalty or GameState.FreeKick;
    public bool OurRestart() => Restart() && Our();
    public bool TheirRestart() => Restart() && !Our();
    public bool Timeout() => GameState == GameState.Timeout;
    public bool OurTimeout() => Timeout() && Our();
    public bool TheirTimeout() => Timeout() && !Our();

    public bool CanMove() => !Halt();
    public bool AllowedNearBall() => Running() || OurRestart() || OurBallPlacement();
    public bool CanKickBall() => Running() || (OurRestart() && Ready);
    public bool ShouldSlowDown() => Stop() || BallPlacement() || CanEnterField();
    public bool CanEnterField() => Timeout();

    public TeamInfo OurInfo() => Config.Common.OurColor == TeamColor.Blue ? BlueInfo : YellowInfo;
    public TeamInfo OppInfo() => Config.Common.OurColor == TeamColor.Yellow ? BlueInfo : YellowInfo;
    
    public float Elapsed() => (float)(DateTime.UtcNow - Time).TotalSeconds;

    public override string ToString()
    {
        return GameState switch
        {
            GameState.None => "None",
            GameState.Halt => "Halt",
            GameState.Timeout => Our() ? "Our timeout" : "Their timeout",
            GameState.Stop => "Stop",
            GameState.BallPlacement => Our() ? "Our ball placement" : "Their ball placement",
            GameState.Kickoff => Our()
                ? (Ready ? "Our kickoff (ready)" : "Our prepare kickoff")
                : (Ready ? "Their kickoff (ready)" : "Their prepare kickoff"),
            GameState.Penalty => Our()
                ? (Ready ? "Our penalty (ready)" : "Our prepare penalty")
                : (Ready ? "Their penalty (ready)" : "Their prepare penalty"),
            GameState.FreeKick => Our() ? "Our free kick" : "Their free kick",
            GameState.Running => "Running",
            _ => "Unknown"
        };
    }
}