using System.Numerics;
using Tracker = Tyr.Common.Data.Ssl.Vision.Tracker;
using Gc = Tyr.Common.Data.Ssl.Gc;
using Tyr.Common.Data.Referee;

namespace Tyr.Referee;

public class Referee
{
    private Tracker.Frame _vision = new();

    private State _state = new();
    public State State => _state;

    private Tracker.Ball? _lastBall;
    private int _moveHysteresis;

    public bool Process(Tracker.Frame? vision, Gc.Referee? gc)
    {
        var oldState = _state;

        if (vision != null)
            _vision = vision;

        if (gc != null)
        {
            _state.Gc = gc;
        }

        if (oldState.Gc.CommandCounter != _state.Gc.CommandCounter)
        {
            Log.ZLogInformation($"received GC command: [{_state.Gc.CommandCounter}] | {_state.Gc.Command}");
            OnNewCommand();
        }

        if (_state.Restart() && BallInPlay())
        {
            _state.GameState = GameState.Running;
        }

        var color = _state.Gc.Command.ToColor();
        if (color.HasValue)
            _state.Color = color.Value;

        if (oldState.GameState != _state.GameState || oldState.Ready != _state.Ready)
        {
            Log.ZLogInformation($"state transition: {oldState} -> {_state}");

            _state.Timestamp = _state.Gc.PacketTimestamp;
            _lastBall = _vision.Ball;
            _moveHysteresis = 0;

            return true;
        }

        return false;
    }

    private bool BallInPlay()
    {
        if (!_state.Ready)
        {
            return false;
        }

        if (_state.Gc.CurrentActionTimeRemaining.Nanoseconds <= 0)
        {
            return true;
        }

        _lastBall ??= _vision.Ball;

        if (!_vision.Ball.HasValue || !_lastBall.HasValue)
        {
            return false;
        }

        const int requiredHys = 5;
        var requiredDis = _state.Our() && _state.Restart() ? 150.0f : 50.0f;

        var ballMoveDis = Vector3.Distance(_vision.Ball.Value.Position, _lastBall.Value.Position);

        if (ballMoveDis > requiredDis)
        {
            _moveHysteresis++;
        }

        return _moveHysteresis >= requiredHys;
    }

    private void OnNewCommand()
    {
        switch (_state.Gc.Command)
        {
            case Gc.Command.Halt:
                _state.GameState = GameState.Halt;
                break;

            case Gc.Command.Stop:
                _state.GameState = GameState.Stop;
                break;

            case Gc.Command.ForceStart:
                _state.GameState = GameState.Running;
                break;

            case Gc.Command.NormalStart when _state.Restart():
                _state.Ready = true;
                break;

            case Gc.Command.PrepareKickoffBlue:
            case Gc.Command.PrepareKickoffYellow:
                _state.GameState = GameState.Kickoff;
                _state.Ready = false;
                break;

            case Gc.Command.PreparePenaltyBlue:
            case Gc.Command.PreparePenaltyYellow:
                _state.GameState = GameState.Penalty;
                _state.Ready = false;
                break;

            case Gc.Command.DirectFreeBlue:
            case Gc.Command.DirectFreeYellow:
                _state.GameState = GameState.FreeKick;
                _state.Ready = true;
                break;

            case Gc.Command.BallPlacementBlue:
            case Gc.Command.BallPlacementYellow:
                _state.GameState = GameState.BallPlacement;
                break;

            case Gc.Command.TimeoutYellow:
            case Gc.Command.TimeoutBlue:
                _state.GameState = GameState.Timeout;
                break;

            case Gc.Command.GoalYellow:
            case Gc.Command.GoalBlue:
                break;

            default:
                throw new ArgumentOutOfRangeException(null, _state.Gc.Command, null);
        }
    }
}