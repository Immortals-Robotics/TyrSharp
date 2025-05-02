using System.Numerics;
using Tyr.Common.Config;
using Tracker = Tyr.Common.Data.Ssl.Vision.Tracker;
using Gc = Tyr.Common.Data.Ssl.Gc;
using Tyr.Common.Data.Referee;

namespace Tyr.Referee;

[Configurable]
public partial class Referee
{
    [ConfigEntry] private static int RequiredHys { get; set; } = 5;
    [ConfigEntry] private static float OurRestartBallMoveDis { get; set; } = 150.0f;
    [ConfigEntry] private static float DefaultBallMoveDis { get; set; } = 50.0f;

    private Tracker.Frame _vision = new();

    public State State { get; private set; } = new();

    private Tracker.Ball? _lastBall;
    private int _moveHysteresis;

    public bool Process(Tracker.Frame? vision, Gc.Referee? gc)
    {
        var oldState = State;

        if (vision != null)
            _vision = vision;

        if (gc != null)
        {
            State = State with { Gc = gc };
        }

        if (oldState.Gc.CommandCounter != State.Gc.CommandCounter)
        {
            Log.ZLogInformation($"received GC command: [{State.Gc.CommandCounter}] | {State.Gc.Command}");
            OnNewCommand();
        }

        if (State.Restart() && BallInPlay())
        {
            State = State with { GameState = GameState.Running };
        }

        var color = State.Gc.Command.ToColor();
        if (color != State.Color)
        {
            State = State with { Color = color };
        }

        if (oldState.GameState != State.GameState ||
            oldState.Ready != State.Ready ||
            oldState.Color != State.Color)
        {
            Log.ZLogInformation($"state transition: {oldState} -> {State}");

            State = State with { Timestamp = State.Gc.PacketTimestamp };
            _lastBall = _vision.Ball;
            _moveHysteresis = 0;

            return true;
        }

        return false;
    }

    private bool BallInPlay()
    {
        if (!State.Ready)
        {
            return false;
        }

        if (State.Gc.CurrentActionTimeRemaining.Nanoseconds <= 0)
        {
            return true;
        }

        _lastBall ??= _vision.Ball;

        if (!_vision.Ball.HasValue || !_lastBall.HasValue)
        {
            return false;
        }

        var requiredDis = State.OurRestart() ? OurRestartBallMoveDis : DefaultBallMoveDis;
        var ballMoveDis = Vector3.Distance(_vision.Ball.Value.Position, _lastBall.Value.Position);
        if (ballMoveDis > requiredDis)
        {
            _moveHysteresis = int.Clamp(_moveHysteresis + 1, 0, RequiredHys);
        }

        return _moveHysteresis >= RequiredHys;
    }

    private void OnNewCommand()
    {
        switch (State.Gc.Command)
        {
            case Gc.Command.Halt:
                State = State with { GameState = GameState.Halt };
                break;

            case Gc.Command.Stop:
                State = State with { GameState = GameState.Stop };
                break;

            case Gc.Command.ForceStart:
                State = State with { GameState = GameState.Running };
                break;

            case Gc.Command.NormalStart when State.Restart():
                State = State with { Ready = true };
                break;

            case Gc.Command.PrepareKickoffBlue:
            case Gc.Command.PrepareKickoffYellow:
                State = State with { GameState = GameState.Kickoff, Ready = false };
                break;

            case Gc.Command.PreparePenaltyBlue:
            case Gc.Command.PreparePenaltyYellow:
                State = State with { GameState = GameState.Penalty, Ready = false };
                break;

            case Gc.Command.DirectFreeBlue:
            case Gc.Command.DirectFreeYellow:
                State = State with { GameState = GameState.FreeKick, Ready = true };
                break;

            case Gc.Command.BallPlacementBlue:
            case Gc.Command.BallPlacementYellow:
                State = State with { GameState = GameState.BallPlacement };
                break;

            case Gc.Command.TimeoutYellow:
            case Gc.Command.TimeoutBlue:
                State = State with { GameState = GameState.Timeout };
                break;

            case Gc.Command.GoalYellow:
            case Gc.Command.GoalBlue:
                break;

            default:
                throw new ArgumentOutOfRangeException(null, State.Gc.Command, null);
        }
    }
}