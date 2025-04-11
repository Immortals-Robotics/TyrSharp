using System.Threading.Channels;
using Tracker = Tyr.Common.Data.Ssl.Vision.Tracker;
using Gc = Tyr.Common.Data.Ssl.Gc;
using Tyr.Common.Data.Referee;
using Tyr.Common.Dataflow;
using Tyr.Common.Module;

namespace Tyr.Referee;

public class Referee : AsyncRunner
{
    private readonly ChannelReader<Gc.Referee> _gcReader;
    private readonly ChannelReader<Tracker.Frame> _visionReader;

    private Tracker.Frame _visionFrame = new();
    private State _state = new();

    private Tracker.Ball _lastBall;
    private int _moveHysteresis;

    private Gc.Referee? _receivedGc;

    public Referee()
    {
        _gcReader = Hub.RawReferee.Subscribe(BroadcastChannel<Gc.Referee>.Mode.All);
        _visionReader = Hub.Vision.Subscribe(BroadcastChannel<Tracker.Frame>.Mode.Latest);
    }

    internal async Task ReceiveGc(CancellationToken token)
    {
        try
        {
            _receivedGc = await _gcReader.ReadAsync(token);

            if (_receivedGc.CommandCounter != _state.Gc.CommandCounter)
            {
                Logger.ZLogInformation($"received GC command: [{_receivedGc.CommandCounter}] | {_receivedGc.Command}");
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    internal async Task ReceiveVision(CancellationToken token)
    {
        try
        {
            _visionFrame = await _visionReader.ReadAsync(token);
        }
        catch (OperationCanceledException)
        {
        }
    }

    internal void Process()
    {
        var oldState = _state;

        if (_receivedGc != null)
        {
            _state.Gc = _receivedGc;
            _receivedGc = null;
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

        Hub.Referee.Publish(_state);
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

    protected override async Task Tick(CancellationToken token)
    {
        await Task.WhenAny(ReceiveGc(token), ReceiveVision(token));

        Process();
    }
}