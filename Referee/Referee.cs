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

        Transition();

        if (oldState.GameState != _state.GameState || oldState.Ready != _state.Ready)
        {
            Logger.ZLogInformation($"state transition: {oldState} -> {_state}");

            _state.Time = DateTime.UtcNow;
            _lastBall = _visionFrame.Ball;
            _moveHysteresis = 0;
        }
    }

    private bool BallMoved()
    {
        const int requiredHys = 5;
        var requiredDis = _state.Our() && _state.Restart() ? 150.0f : 50.0f;

        var ballMoveDis = _visionFrame.Ball.Position.DistanceTo(_lastBall.Position);
        Logger.ZLogDebug($"ball has moved {ballMoveDis}");

        if (ballMoveDis > requiredDis)
        {
            _moveHysteresis++;
        }

        return _moveHysteresis >= requiredHys;
    }

    private void Transition()
    {
    }
}