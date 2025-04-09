using System.Threading.Channels;
using Tracker = Tyr.Common.Data.Ssl.Vision.Tracker;
using Gc = Tyr.Common.Data.Ssl.Gc;
using Tyr.Common.Data.Referee;
using Tyr.Common.Dataflow;

namespace Tyr.Referee;

public class Referee
{
    private ChannelReader<Gc.Referee> _gcReader;
    private ChannelReader<Tracker.Frame> _visionReader;

    private Tracker.Frame _visionFrame = new();
    private State _state = new();

    private Tracker.Ball _lastBall;
    private int _moveHysteresis;

    private Gc.Referee? _receivedGc;

    internal Referee()
    {
        _gcReader = Hub.Gc.Subscribe(BroadcastChannel<Gc.Referee>.Mode.All);
        _visionReader = Hub.Vision.Subscribe(BroadcastChannel<Tracker.Frame>.Mode.Latest);
    }

    internal bool ReceiveGc()
    {
        if (!_gcReader.TryRead(out _receivedGc)) return false;

        if (_receivedGc != null && _state.Gc.CommandCounter != _receivedGc.CommandCounter)
        {
            Logger.ZLogInformation($"received GC command: [{_receivedGc.CommandCounter}] | {_receivedGc.Command}");
        }

        return true;
    }

    internal bool ReceiveVision()
    {
        if (!_visionReader.TryRead(out var visionFrame)) return false;

        _visionFrame = visionFrame;
        return true;
    }

    public void Process()
    {
        var oldState = _state;

        if (_receivedGc != null)
        {
            _state.Gc = _receivedGc;
        }

        if (oldState.Gc.CommandCounter != _state.Gc.CommandCounter)
        {
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
            Logger.ZLogInformation($"state transition: {oldState} -> {_state}");

            _state.Time = DateTime.UtcNow;
            _lastBall = _visionFrame.Ball;
            _moveHysteresis = 0;
        }
    }

    private bool BallInPlay()
    {
        if (!_state.Ready)
        {
            return false;
        }

        if (_state.Gc.CurrentActionTimeRemaining < 0f)
        {
            return true;
        }

        const int requiredHys = 5;
        var requiredDis = _state.Our() && _state.Restart() ? 150.0f : 50.0f;

        var ballMoveDis = _visionFrame.Ball.Position.DistanceTo(_lastBall.Position);

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
                throw new ArgumentOutOfRangeException("Command", _state.Gc.Command, null);
        }
    }
}